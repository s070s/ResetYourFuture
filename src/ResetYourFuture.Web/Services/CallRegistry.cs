using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// In-memory participant status within a <see cref="CallState"/>. Separate from
/// <see cref="ResetYourFuture.Domain.Enums.CallParticipantStatus"/> — this one only needs to
/// distinguish the states the registry itself must reason about.
/// </summary>
public enum RegistryParticipantState
{
    Invited,
    Joined,
    Disconnected
}

/// <summary>
/// A single participant's live registry state within a call.
/// </summary>
public sealed class CallParticipantState
{
    public required string UserId { get; init; }
    public string? ConnectionId { get; set; }
    public RegistryParticipantState State { get; set; } = RegistryParticipantState.Invited;
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DisconnectedAt { get; set; }
    public bool MicOn { get; set; } = true;
    public bool CameraOn { get; set; } = true;
    public bool ScreenSharing { get; set; }
}

/// <summary>
/// In-memory state for a single active call.
/// </summary>
public sealed class CallState
{
    public required Guid CallId { get; init; }
    public required string InitiatorId { get; init; }
    public Guid? ConversationId { get; init; }
    public bool IsGroup { get; init; }
    public Dictionary<string, CallParticipantState> Participants { get; } = [];

    /// <summary>
    /// True once 2+ participants were simultaneously Joined at some point. Sticky — unlike
    /// Participants, this does not revert to false as people leave, so end-of-call logic can
    /// still tell "was actually connected, then everyone left" apart from "never connected"
    /// (Missed) after participants have already been removed from the map.
    /// </summary>
    public bool HasConnected { get; set; }
}

/// <summary>
/// Pure, lock-protected, in-memory state machine for active calls. Has zero external
/// dependencies (no DB, no SignalR) so it is fully unit-testable; the hub/background-service
/// layer is responsible for translating its decisions into DB writes and client notifications.
///
/// Presence: a userId is "connected" as soon as it has at least one tracked call-hub
/// connection (added in <see cref="AddUserConnection"/>/removed in <see cref="RemoveUserConnection"/>),
/// independent of whether that user is in any call. This lets <c>StartCall</c> tell online
/// apart from offline callees without querying SignalR group membership (which isn't queryable).
/// Add/Remove report offline↔online transitions so the hub broadcasts presence only when a
/// user's first connection opens or last connection closes, not per tab.
/// </summary>
public class CallRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, CallState> _calls = [];
    private readonly Dictionary<string, int> _connectedUserRefCounts = [];

    // --- Presence (call-hub connection tracking) ---

    /// <summary>True if this was the user's first tracked connection (offline → online transition).</summary>
    public bool AddUserConnection(string userId)
    {
        lock (_lock)
        {
            var count = _connectedUserRefCounts.GetValueOrDefault(userId);
            _connectedUserRefCounts[userId] = count + 1;
            return count == 0;
        }
    }

    /// <summary>True if this was the user's last tracked connection (online → offline transition).</summary>
    public bool RemoveUserConnection(string userId)
    {
        lock (_lock)
        {
            if (!_connectedUserRefCounts.TryGetValue(userId, out var count))
                return false;

            if (count <= 1)
            {
                _connectedUserRefCounts.Remove(userId);
                return true;
            }

            _connectedUserRefCounts[userId] = count - 1;
            return false;
        }
    }

    public List<string> GetOnlineUserIds()
    {
        lock (_lock)
        {
            return [.. _connectedUserRefCounts.Keys];
        }
    }

    public bool IsUserOnline(string userId)
    {
        lock (_lock)
        {
            return _connectedUserRefCounts.ContainsKey(userId);
        }
    }

    // --- Call lifecycle ---

    /// <summary>
    /// True if the user is currently invited-to or joined-in any active call.
    /// </summary>
    public bool IsUserBusy(string userId)
    {
        lock (_lock)
        {
            return _calls.Values.Any(c => c.Participants.TryGetValue(userId, out var p)
                && p.State is RegistryParticipantState.Invited or RegistryParticipantState.Joined);
        }
    }

    /// <summary>
    /// Creates a new call with the initiator already joined (connected via <paramref name="initiatorConnectionId"/>)
    /// and the given invitees in the Invited state. Returns false if a call with this id already exists.
    /// </summary>
    public bool TryCreateCall(
        Guid callId,
        string initiatorId,
        string initiatorConnectionId,
        Guid? conversationId,
        IEnumerable<string> inviteeIds,
        bool isGroup)
    {
        lock (_lock)
        {
            if (_calls.ContainsKey(callId))
                return false;

            var call = new CallState
            {
                CallId = callId,
                InitiatorId = initiatorId,
                ConversationId = conversationId,
                IsGroup = isGroup
            };

            call.Participants[initiatorId] = new CallParticipantState
            {
                UserId = initiatorId,
                ConnectionId = initiatorConnectionId,
                State = RegistryParticipantState.Joined
            };

            foreach (var inviteeId in inviteeIds.Distinct())
            {
                call.Participants[inviteeId] = new CallParticipantState
                {
                    UserId = inviteeId,
                    State = RegistryParticipantState.Invited
                };
            }

            _calls[callId] = call;
            return true;
        }
    }

    /// <summary>
    /// Adds a mid-call invite (group calls / InviteToCall). Returns false if the call doesn't
    /// exist, the user is already a member, or the participant cap would be exceeded.
    /// </summary>
    public bool TryAddInvite(Guid callId, string userId, int maxParticipants)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return false;

            if (call.Participants.ContainsKey(userId))
                return false;

            if (call.Participants.Count >= maxParticipants)
                return false;

            call.Participants[userId] = new CallParticipantState
            {
                UserId = userId,
                State = RegistryParticipantState.Invited
            };

            return true;
        }
    }

    /// <summary>
    /// Marks the participant Joined and keys them to the accepting connection. Returns false if
    /// the call doesn't exist or the user has no pending invite in it.
    /// </summary>
    public bool TryAccept(Guid callId, string userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return false;

            if (!call.Participants.TryGetValue(userId, out var participant))
                return false;

            participant.ConnectionId = connectionId;
            participant.State = RegistryParticipantState.Joined;
            participant.DisconnectedAt = null;

            if (call.Participants.Values.Count(p => p.State == RegistryParticipantState.Joined) >= 2)
                call.HasConnected = true;

            return true;
        }
    }

    public bool TryDecline(Guid callId, string userId)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return false;

            return call.Participants.Remove(userId);
        }
    }

    /// <summary>
    /// Removes a joined/invited participant from the call (leave, cancel-by-initiator, etc.).
    /// </summary>
    public bool TryLeave(Guid callId, string userId)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return false;

            return call.Participants.Remove(userId);
        }
    }

    /// <summary>
    /// Re-keys a participant's connection on SignalR reconnect (RejoinCall). Returns false if the
    /// call or the participant no longer exists, so the caller can tell the client to tear down.
    /// </summary>
    public bool TryUpdateConnection(Guid callId, string userId, string newConnectionId)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return false;

            if (!call.Participants.TryGetValue(userId, out var participant))
                return false;

            participant.ConnectionId = newConnectionId;
            participant.State = RegistryParticipantState.Joined;
            participant.DisconnectedAt = null;

            if (call.Participants.Values.Count(p => p.State == RegistryParticipantState.Joined) >= 2)
                call.HasConnected = true;

            return true;
        }
    }

    public void SetMediaState(Guid callId, string userId, bool micOn, bool cameraOn, bool screenSharing)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return;

            if (!call.Participants.TryGetValue(userId, out var participant))
                return;

            participant.MicOn = micOn;
            participant.CameraOn = cameraOn;
            participant.ScreenSharing = screenSharing;
        }
    }

    /// <summary>
    /// Marks a connection as disconnected (grace period starts), rather than removing it
    /// outright — <see cref="CallRingMonitor"/>'s poll loop treats a disconnected participant as
    /// Left once the grace window has elapsed without a reconnect (<see cref="TryUpdateConnection"/>,
    /// checked via <see cref="TakeExpiredDisconnects"/>). Returns the callIds where this connectionId
    /// was found.
    /// </summary>
    public List<Guid> HandleDisconnect(string connectionId)
    {
        lock (_lock)
        {
            var affected = new List<Guid>();
            foreach (var call in _calls.Values)
            {
                foreach (var participant in call.Participants.Values)
                {
                    if (participant.ConnectionId == connectionId && participant.State == RegistryParticipantState.Joined)
                    {
                        participant.State = RegistryParticipantState.Disconnected;
                        participant.DisconnectedAt = DateTime.UtcNow;
                        affected.Add(call.CallId);
                    }
                }
            }
            return affected;
        }
    }

    /// <summary>
    /// Removes participants that have been Disconnected for longer than <paramref name="graceSeconds"/>,
    /// treating them as having Left. Returns the (callId, userId) pairs that were dropped so the
    /// caller can persist Left + notify + re-check end conditions.
    /// </summary>
    public List<(Guid CallId, string UserId)> TakeExpiredDisconnects(int graceSeconds)
    {
        lock (_lock)
        {
            var expired = new List<(Guid, string)>();
            var cutoff = DateTime.UtcNow.AddSeconds(-graceSeconds);

            foreach (var call in _calls.Values)
            {
                foreach (var userId in call.Participants
                    .Where(kv => kv.Value.State == RegistryParticipantState.Disconnected
                              && kv.Value.DisconnectedAt is not null
                              && kv.Value.DisconnectedAt.Value <= cutoff)
                    .Select(kv => kv.Key)
                    .ToList())
                {
                    call.Participants.Remove(userId);
                    expired.Add((call.CallId, userId));
                }
            }

            return expired;
        }
    }

    /// <summary>
    /// Removes invites older than <paramref name="ringTimeoutSeconds"/> (never accepted/declined).
    /// Returns the (callId, userId) pairs that expired so the caller can mark them Missed.
    /// </summary>
    public List<(Guid CallId, string UserId)> TakeExpiredInvites(int ringTimeoutSeconds)
    {
        lock (_lock)
        {
            var expired = new List<(Guid, string)>();
            var cutoff = DateTime.UtcNow.AddSeconds(-ringTimeoutSeconds);

            foreach (var call in _calls.Values)
            {
                foreach (var userId in call.Participants
                    .Where(kv => kv.Value.State == RegistryParticipantState.Invited && kv.Value.InvitedAt <= cutoff)
                    .Select(kv => kv.Key)
                    .ToList())
                {
                    call.Participants.Remove(userId);
                    expired.Add((call.CallId, userId));
                }
            }

            return expired;
        }
    }

    /// <summary>
    /// Pure decision of whether a call should end: fewer than 2 Joined participants remain and
    /// no invites are still pending (ringing). Kept here — rather than in the hub/monitor — so the
    /// "should this call end" logic stays testable without DB/hub mocks; callers perform the
    /// actual DB write + broadcast themselves.
    /// </summary>
    public bool ShouldEndCall(Guid callId)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return false;

            var joinedCount = call.Participants.Values.Count(p => p.State == RegistryParticipantState.Joined);
            var hasPendingInvites = call.Participants.Values.Any(p => p.State == RegistryParticipantState.Invited);

            return joinedCount < 2 && !hasPendingInvites;
        }
    }

    /// <summary>
    /// UserIds still in the Invited (ringing) state for a call — the participants a forced
    /// call-end must notify individually, since they never joined the call's SignalR group.
    /// </summary>
    public List<string> GetInvitedUserIds(Guid callId)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return [];

            return call.Participants.Values
                .Where(p => p.State == RegistryParticipantState.Invited)
                .Select(p => p.UserId)
                .ToList();
        }
    }

    public bool IsMemberConnection(Guid callId, string connectionId)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return false;

            return call.Participants.Values.Any(p => p.ConnectionId == connectionId
                && p.State == RegistryParticipantState.Joined);
        }
    }

    public bool CallExists(Guid callId)
    {
        lock (_lock)
        {
            return _calls.ContainsKey(callId);
        }
    }

    public List<CallParticipantDto> GetJoinedParticipants(Guid callId, string? excludingUserId = null)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return [];

            return call.Participants.Values
                .Where(p => p.State == RegistryParticipantState.Joined
                    && p.ConnectionId is not null
                    && p.UserId != excludingUserId)
                .Select(p => new CallParticipantDto(p.UserId, p.UserId, p.ConnectionId!, p.MicOn, p.CameraOn, p.ScreenSharing))
                .ToList();
        }
    }

    public int ParticipantCount(Guid callId)
    {
        lock (_lock)
        {
            return _calls.TryGetValue(callId, out var call) ? call.Participants.Count : 0;
        }
    }

    public CallState? GetCall(Guid callId)
    {
        lock (_lock)
        {
            if (!_calls.TryGetValue(callId, out var call))
                return null;

            // Defensive copy isn't needed by current callers (read-only inspection under test),
            // but callers must not mutate the returned instance outside the lock.
            return call;
        }
    }

    public void RemoveCall(Guid callId)
    {
        lock (_lock)
        {
            _calls.Remove(callId);
        }
    }

    public List<Guid> GetAllCallIds()
    {
        lock (_lock)
        {
            return _calls.Keys.ToList();
        }
    }
}
