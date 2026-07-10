using ResetYourFuture.Web.Interfaces;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Circuit-scoped presence state for the UI: which users are online right now, plus live
/// last-seen overrides carried on <c>PresenceChanged</c> events. Seeded from the call hub's
/// <c>GetOnlineUsers</c> snapshot once this circuit's <see cref="ICallService"/> connection is up
/// (CallOverlayHost establishes it globally), and re-seeded on every reconnect because events
/// were missed while the connection was down. Components subscribe to <see cref="Changed"/>
/// and re-render; <see cref="Changed"/> fires on SignalR background threads, so component
/// handlers must marshal via InvokeAsync.
/// </summary>
public class PresenceService : IDisposable
{
    private readonly ICallService _callService;
    private readonly Lock _gate = new();
    private readonly HashSet<string> _online = [];
    private readonly Dictionary<string, DateTime> _lastSeenOverrides = [];
    private bool _seeded;
    private bool _wasConnected;

    public event Action? Changed;

    public PresenceService(ICallService callService)
    {
        _callService = callService;
        _callService.PresenceChanged += HandlePresenceChanged;
        _callService.StateChanged += HandleStateChanged;
        _wasConnected = _callService.IsConnected;
    }

    public bool IsOnline(string userId)
    {
        lock (_gate)
        {
            return _online.Contains(userId);
        }
    }

    /// <summary>A live override from a PresenceChanged event wins over the caller's DTO fallback.</summary>
    public DateTime? GetLastSeen(string userId, DateTime? fallback)
    {
        lock (_gate)
        {
            return _lastSeenOverrides.TryGetValue(userId, out var ts) ? ts : fallback;
        }
    }

    /// <summary>
    /// Idempotent. No-ops while the hub isn't connected — <see cref="HandleStateChanged"/>
    /// seeds as soon as it is, so callers don't need to retry.
    /// </summary>
    public async Task EnsureSeededAsync()
    {
        if (_seeded || !_callService.IsConnected)
            return;

        await SeedAsync();
    }

    private async Task SeedAsync()
    {
        var ids = await _callService.GetOnlineUsersAsync();
        lock (_gate)
        {
            _online.Clear();
            foreach (var id in ids)
                _online.Add(id);
            _seeded = true;
        }
        Changed?.Invoke();
    }

    private void HandlePresenceChanged(string userId, bool isOnline, DateTime? lastSeenUtc)
    {
        lock (_gate)
        {
            if (isOnline)
                _online.Add(userId);
            else
                _online.Remove(userId);

            if (lastSeenUtc is not null)
                _lastSeenOverrides[userId] = lastSeenUtc.Value;
        }
        Changed?.Invoke();
    }

    private void HandleStateChanged()
    {
        var connected = _callService.IsConnected;
        var transitioned = connected && !_wasConnected;
        _wasConnected = connected;
        if (!transitioned)
            return;

        // Fire-and-forget: PresenceChanged events were missed during the disconnect window,
        // so refresh the whole snapshot; Changed fires when it lands.
        _ = ReseedSafelyAsync();
    }

    private async Task ReseedSafelyAsync()
    {
        try
        {
            await SeedAsync();
        }
        catch
        {
            // Best-effort: live PresenceChanged events still apply, and the next connect
            // transition (or EnsureSeededAsync while still unseeded) retries the snapshot.
        }
    }

    public void Dispose()
    {
        _callService.PresenceChanged -= HandlePresenceChanged;
        _callService.StateChanged -= HandleStateChanged;
    }
}
