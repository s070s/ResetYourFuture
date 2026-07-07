using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Web.Services;

namespace ResetYourFuture.Web.Hubs;

/// <summary>
/// SignalR hub for call signaling (ring/accept/decline/leave + WebRTC offer/answer/ICE relay).
/// Mirrors <see cref="ChatHub"/>'s connection/group pattern: every connection joins
/// <c>user_{userId}</c> on connect, and additionally <c>call_{callId}</c> groups while a member
/// of that call. All actual media never touches the server — this hub only relays signaling
/// messages and tracks who is in which call via <see cref="CallRegistry"/>.
/// </summary>
[Authorize]
public partial class CallHub : Hub
{
    private readonly ICallEventService _callEventService;
    private readonly ICallQueryService _callQueryService;
    private readonly CallRegistry _registry;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly WebRtcOptions _options;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly ILogger<CallHub> _logger;

    public CallHub(
        ICallEventService callEventService,
        ICallQueryService callQueryService,
        CallRegistry registry,
        UserManager<ApplicationUser> userManager,
        IOptions<WebRtcOptions> options,
        IHubContext<ChatHub> chatHub,
        ILogger<CallHub> logger)
    {
        _callEventService = callEventService;
        _callQueryService = callQueryService;
        _registry = registry;
        _userManager = userManager;
        _options = options.Value;
        _chatHub = chatHub;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || !user.IsEnabled)
            {
                _logger.LogWarning("Call: Rejected connection for disabled/unknown user {UserId}.", userId);
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _registry.AddUserConnection(userId);
            _logger.LogInformation("Call: User {UserId} connected.", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _registry.RemoveUserConnection(userId);

            // Mark any call membership as Disconnected. CallRegistry starts a 15s reconnect grace
            // (DisconnectedAt) rather than removing the participant immediately — CallRingMonitor's
            // poll loop (TakeExpiredDisconnects) is what turns a still-disconnected participant
            // into a persisted Left + end-condition check once the grace window elapses. This
            // keeps CallRegistry itself free of timers/background work.
            var affectedCalls = _registry.HandleDisconnect(Context.ConnectionId);
            foreach (var callId in affectedCalls)
            {
                await Clients.Group($"call_{callId}").SendAsync("ParticipantLeft", callId, userId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<StartCallResultDto> StartCall(List<string> calleeIds, Guid? conversationId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return new StartCallResultDto(null, StartCallStatus.NoAccess, []);

        var isAdmin = Context.User?.IsInRole("Admin") == true;
        if (!await _callQueryService.HasCallAccessAsync(userId, isAdmin))
            return new StartCallResultDto(null, StartCallStatus.NoAccess, []);

        if (_registry.IsUserBusy(userId))
            return new StartCallResultDto(null, StartCallStatus.CallerBusy, []);

        var distinctCallees = calleeIds.Where(id => id != userId).Distinct().ToList();
        if (distinctCallees.Count == 0)
            return new StartCallResultDto(null, StartCallStatus.AllUnavailable, []);

        // Cap total participants (initiator + invitees) at MaxParticipants — truncate the callee
        // list rather than rejecting outright, since the initiator is always included.
        var capacity = Math.Max(0, _options.MaxParticipants - 1);
        if (distinctCallees.Count > capacity)
            distinctCallees = distinctCallees.Take(capacity).ToList();

        var onlineCallees = distinctCallees.Where(id => _registry.IsUserOnline(id)).ToList();
        var unavailableIds = distinctCallees.Except(onlineCallees).ToList();

        var isGroup = distinctCallees.Count > 1;

        if (onlineCallees.Count == 0)
        {
            if (!isGroup && conversationId is not null)
            {
                await RecordAndEndImmediateMiss(userId, distinctCallees[0], conversationId);
            }
            return new StartCallResultDto(null, StartCallStatus.AllUnavailable, unavailableIds);
        }

        var session = await _callEventService.CreateSessionAsync(userId, conversationId, onlineCallees);

        var created = _registry.TryCreateCall(session.Id, userId, Context.ConnectionId, conversationId, onlineCallees, isGroup);
        if (!created)
        {
            _logger.LogError("Call: TryCreateCall failed for new session {CallId} (id collision).", session.Id);
            return new StartCallResultDto(null, StartCallStatus.AllUnavailable, unavailableIds);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"call_{session.Id}");

        var initiator = await _userManager.FindByIdAsync(userId);
        var initiatorName = initiator is not null ? $"{initiator.FirstName} {initiator.LastName}" : "Unknown";
        var participantNames = await ResolveDisplayNames(onlineCallees.Append(userId));

        var invite = new CallInviteDto(session.Id, userId, initiatorName, conversationId, isGroup, participantNames);

        foreach (var calleeId in onlineCallees)
        {
            await Clients.Group($"user_{calleeId}").SendAsync("IncomingCall", invite);
        }

        return new StartCallResultDto(session.Id, StartCallStatus.Ringing, unavailableIds);
    }

    public async Task<JoinCallResultDto?> AcceptCall(Guid callId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return null;

        if (!_registry.TryAccept(callId, userId, Context.ConnectionId))
            return null;

        await Clients.Group($"user_{userId}").SendAsync("IncomingCallHandled", callId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"call_{callId}");

        await _callEventService.MarkParticipantAsync(callId, userId, CallParticipantStatus.Joined);

        var call = _registry.GetCall(callId);
        if (call is not null && !call.IsGroup)
        {
            // A 1:1 call has exactly 2 participants (initiator + sole callee); the initiator is
            // already Joined from TryCreateCall, so the callee's accept is what brings the count
            // to 2 — i.e. this fires exactly once, on the only possible accept for a 1:1 call.
            var joinedCount = call.Participants.Values.Count(p => p.State == RegistryParticipantState.Joined);
            if (joinedCount == 2)
            {
                var chatMessage = await _callEventService.RecordChatEventAsync(callId, CallEventKind.Started);
                if (chatMessage is not null)
                {
                    await BroadcastChatEvent(call, chatMessage);
                }
            }
        }

        var existingParticipants = await ResolveParticipantNames(_registry.GetJoinedParticipants(callId, excludingUserId: userId));

        var displayName = await ResolveDisplayName(userId);
        await Clients.Group($"call_{callId}").SendAsync(
            "ParticipantJoined",
            callId,
            new CallParticipantDto(userId, displayName, Context.ConnectionId, true, true, false));

        return new JoinCallResultDto(callId, existingParticipants);
    }

    public async Task DeclineCall(Guid callId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return;

        if (!_registry.TryDecline(callId, userId))
            return;

        await _callEventService.MarkParticipantAsync(callId, userId, CallParticipantStatus.Declined);
        await Clients.Group($"call_{callId}").SendAsync("CallDeclined", callId, userId);

        await EndCallIfNeeded(callId, CallEndReason.Declined);
    }

    /// <summary>
    /// Withdraws a call that hasn't connected yet (initiator backs out while still ringing).
    /// Unlike LeaveCall/DeclineCall, this always ends the call outright rather than deferring to
    /// <see cref="CallRegistry.ShouldEndCall"/> — a group call with several invitees still ringing
    /// must not be left dangling just because some invites are still pending when the initiator
    /// who started it withdraws.
    /// </summary>
    public async Task CancelCall(Guid callId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return;

        var call = _registry.GetCall(callId);
        if (call is null || !_registry.TryLeave(callId, userId))
            return;

        await _callEventService.MarkParticipantAsync(callId, userId, CallParticipantStatus.Left);
        await Clients.Group($"call_{callId}").SendAsync("CallCancelled", callId, userId);

        await ForceEndCall(callId, call, CallEndReason.Cancelled);
    }

    public async Task LeaveCall(Guid callId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return;

        if (!_registry.TryLeave(callId, userId))
            return;

        await _callEventService.MarkParticipantAsync(callId, userId, CallParticipantStatus.Left);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"call_{callId}");
        await Clients.Group($"call_{callId}").SendAsync("ParticipantLeft", callId, userId);

        await EndCallIfNeeded(callId, CallEndReason.Completed);
    }

    public async Task InviteToCall(Guid callId, string userId)
    {
        var callerId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(callerId) || string.IsNullOrEmpty(userId))
            return;

        if (!_registry.IsMemberConnection(callId, Context.ConnectionId))
            return;

        if (_registry.IsUserBusy(userId) || !_registry.IsUserOnline(userId))
        {
            await Clients.Caller.SendAsync("CallError", "That user is unavailable.");
            return;
        }

        if (!_registry.TryAddInvite(callId, userId, _options.MaxParticipants))
        {
            await Clients.Caller.SendAsync("CallError", "The call is full.");
            return;
        }

        var caller = await _userManager.FindByIdAsync(callerId);
        var callerName = caller is not null ? $"{caller.FirstName} {caller.LastName}" : "Unknown";
        var call = _registry.GetCall(callId);
        var participantNames = call is not null
            ? await ResolveDisplayNames(call.Participants.Keys)
            : [];

        var invite = new CallInviteDto(callId, callerId, callerName, call?.ConversationId, true, participantNames);
        await Clients.Group($"user_{userId}").SendAsync("IncomingCall", invite);
    }

    public async Task<JoinCallResultDto?> RejoinCall(Guid callId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return null;

        if (!_registry.CallExists(callId) || !_registry.TryUpdateConnection(callId, userId, Context.ConnectionId))
            return null;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"call_{callId}");
        await Clients.Group($"call_{callId}").SendAsync("ParticipantReconnected", callId, userId);

        var existingParticipants = await ResolveParticipantNames(_registry.GetJoinedParticipants(callId, excludingUserId: userId));
        return new JoinCallResultDto(callId, existingParticipants);
    }

    public async Task UpdateMediaState(Guid callId, MediaStateDto state)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return;

        _registry.SetMediaState(callId, userId, state.MicOn, state.CameraOn, state.ScreenSharing);
        await Clients.Group($"call_{callId}").SendAsync("ParticipantMediaChanged", callId, userId, state);
    }

    public async Task<List<ChatUserDto>> GetCallableUsers(string? search)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return [];

        return await _callQueryService.GetCallableUsersAsync(userId, search);
    }

    // --- Shared helpers (used by signaling partial + ring monitor callers) ---

    internal async Task EndCallIfNeeded(Guid callId, CallEndReason reasonIfEverConnected)
    {
        if (!_registry.ShouldEndCall(callId))
            return;

        var call = _registry.GetCall(callId);
        if (call is null)
            return;

        await ForceEndCall(callId, call, reasonIfEverConnected);
    }

    private async Task ForceEndCall(Guid callId, CallState call, CallEndReason reasonIfEverConnected)
    {
        var reason = call.HasConnected ? reasonIfEverConnected : CallEndReason.Missed;

        await _callEventService.EndSessionAsync(callId, reason);

        if (!call.IsGroup)
        {
            var kind = reason == CallEndReason.Missed ? CallEventKind.Missed : CallEventKind.Ended;
            var chatMessage = await _callEventService.RecordChatEventAsync(callId, kind);
            if (chatMessage is not null)
            {
                await BroadcastChatEvent(call, chatMessage);
            }
        }

        await Clients.Group($"call_{callId}").SendAsync("CallEnded", callId, reason);
        _registry.RemoveCall(callId);
    }

    private async Task RecordAndEndImmediateMiss(string initiatorId, string calleeId, Guid? conversationId)
    {
        var session = await _callEventService.CreateSessionAsync(initiatorId, conversationId, [calleeId]);
        await _callEventService.MarkParticipantAsync(session.Id, calleeId, CallParticipantStatus.Missed);
        await _callEventService.EndSessionAsync(session.Id, CallEndReason.Missed);

        var chatMessage = await _callEventService.RecordChatEventAsync(session.Id, CallEventKind.Missed);
        if (chatMessage is not null)
        {
            await _chatHub.Clients.Group($"user_{initiatorId}").SendAsync("ReceiveMessage", chatMessage);
            await _chatHub.Clients.Group($"user_{calleeId}").SendAsync("ReceiveMessage", chatMessage);
        }
    }

    private async Task BroadcastChatEvent(CallState call, ChatMessageDto chatMessage)
    {
        // Include InitiatorId explicitly: by the time a call ends, the initiator may already have
        // been removed from Participants (e.g. CancelCall removes them before ending the call), but
        // they should still see the live chat update in their own conversation pane.
        var recipientIds = call.Participants.Keys.Append(call.InitiatorId).Distinct();
        foreach (var participantId in recipientIds)
        {
            await _chatHub.Clients.Group($"user_{participantId}").SendAsync("ReceiveMessage", chatMessage);
        }
    }

    private async Task<string> ResolveDisplayName(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user is not null ? $"{user.FirstName} {user.LastName}" : "Unknown";
    }

    private async Task<List<string>> ResolveDisplayNames(IEnumerable<string> userIds)
    {
        var names = new List<string>();
        foreach (var userId in userIds.Distinct())
        {
            names.Add(await ResolveDisplayName(userId));
        }
        return names;
    }

    /// <summary>
    /// CallRegistry has no identity/display-name concept (kept pure/zero-deps), so its
    /// CallParticipantDto entries carry the userId as a placeholder DisplayName — this fills in
    /// the real name before the DTO reaches the client.
    /// </summary>
    private async Task<List<CallParticipantDto>> ResolveParticipantNames(List<CallParticipantDto> participants)
    {
        var result = new List<CallParticipantDto>(participants.Count);
        foreach (var p in participants)
        {
            result.Add(p with { DisplayName = await ResolveDisplayName(p.UserId) });
        }
        return result;
    }
}
