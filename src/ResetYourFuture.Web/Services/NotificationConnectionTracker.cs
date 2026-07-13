using System.Collections.Concurrent;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Tracks which users have at least one live <c>NotificationHub</c> connection (a tab open on
/// the app right now — the hub connects globally in <c>MainLayout</c> for every authenticated
/// user, so this doubles as a general online/offline signal). Reference-counted per user
/// because a user can have multiple tabs/circuits open at once.
/// </summary>
public sealed class NotificationConnectionTracker
{
    private readonly ConcurrentDictionary<string, int> _connectionCounts = new();

    public void MarkConnected(string userId) =>
        _connectionCounts.AddOrUpdate(userId, 1, (_, count) => count + 1);

    public void MarkDisconnected(string userId) =>
        _connectionCounts.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1));

    public bool IsOnline(string userId) =>
        _connectionCounts.TryGetValue(userId, out var count) && count > 0;
}
