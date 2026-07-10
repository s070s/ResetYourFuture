using ResetYourFuture.Application.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Interfaces;

/// <summary>
/// Where a call is in its lifecycle, from this client's point of view.
/// </summary>
public enum CallStage
{
    Idle,
    Outgoing,
    Incoming,
    Connecting,
    InCall
}

/// <summary>
/// Live, mutable view of one remote participant for the UI (tiles, badges, spinners).
/// Distinct from the server-side <c>CallRegistry</c>'s own (immutable-shape) participant
/// record — this one also tracks the local RTCPeerConnection's connection state.
/// </summary>
public class CallParticipantView
{
    public required string UserId { get; init; }
    public required string DisplayName { get; set; }
    public required string ConnectionId { get; set; }
    public bool MicOn { get; set; } = true;
    public bool CameraOn { get; set; } = true;
    public bool ScreenSharing { get; set; }

    /// <summary>RTCPeerConnection.connectionState ("new"/"connecting"/"connected"/"disconnected"/"failed"/"closed").</summary>
    public string PeerConnectionState { get; set; } = "new";
}

/// <summary>
/// Client service for video calls (SignalR signaling + WebRTC media orchestration via JS interop).
/// Hub-only — no REST endpoints — so it holds no HttpClient.
/// </summary>
public interface ICallService : IAsyncDisposable
{
    /// <summary>Fired whenever any state exposed by this service changes.</summary>
    event Action? StateChanged;

    /// <summary>Fired on a recoverable error (media permission denied, hub rejection, etc.) with a user-facing-ish message/key.</summary>
    event Action<string>? ErrorOccurred;

    /// <summary>Fired when a page wants the group-call user picker opened (e.g. from the conversation sidebar).</summary>
    event Action? GroupCallPickerRequested;

    /// <summary>Server presence transition relayed from the hub: (userId, isOnline, lastSeenAtUtc).</summary>
    event Action<string, bool, DateTime?>? PresenceChanged;

    CallStage Stage { get; }
    CallInviteDto? IncomingInvite { get; }
    Guid? ActiveCallId { get; }
    IReadOnlyCollection<CallParticipantView> Participants { get; }
    bool MicOn { get; }
    bool CameraOn { get; }
    bool ScreenSharing { get; }
    bool IsConnected { get; }

    /// <summary>Server-configured cap on total participants (initiator + invitees) per call.</summary>
    int MaxParticipants { get; }

    /// <summary>Idempotent — connects the call hub once per circuit. Safe to call from every page.</summary>
    Task EnsureConnectedAsync(ClaimsPrincipal? circuitUser = null);

    Task StartCallAsync(List<string> userIds, Guid? conversationId = null);
    Task AcceptAsync();
    Task DeclineAsync();
    Task CancelAsync();
    Task HangUpAsync();
    Task InviteAsync(string userId);

    Task ToggleMicAsync();
    Task ToggleCameraAsync();
    Task ToggleScreenShareAsync();

    Task<List<ChatUserDto>> GetCallableUsersAsync(string? search = null);

    /// <summary>Snapshot of currently-online userIds; empty when the hub is not connected.</summary>
    Task<List<string>> GetOnlineUsersAsync();

    Task BindVideoAsync(string connectionId, string elementId);
    Task BindLocalVideoAsync(string elementId);

    void RequestGroupCallPicker();
}
