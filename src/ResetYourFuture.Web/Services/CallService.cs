using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Web.Interfaces;
using System.Security.Claims;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Client call service: SignalR signaling (<c>/hubs/call</c>) plus WebRTC media orchestration via
/// <c>window.webrtcInterop</c> (see webrtc-interop.js, WP4). Mirrors <see cref="ChatService"/>'s hub
/// bootstrapping (hub URL captured from <c>IHttpContextAccessor</c> before circuit transition, fresh
/// token per (re)connect since HttpContext is null inside the circuit).
///
/// Mesh-join convention: whoever just joined (accept, rejoin, or a mid-call invite acceptance) offers
/// to every participant already in the call; existing participants only ever answer. This means the
/// side creating a peer connection knows up front whether it will be initiating the offer.
///
/// Registered AddScoped (one instance per circuit) — never disposed by pages; the DI container disposes
/// it when the circuit's scope ends.
/// </summary>
public partial class CallService : ICallService
{
    private readonly IAuthService _authService;
    private readonly IJSRuntime _js;
    private readonly IOptions<WebRtcOptions> _webRtcOptions;
    private readonly ILogger<CallService> _logger;
    private readonly string _hubUrl;

    private HubConnection? _hub;
    private readonly Dictionary<string, CallParticipantView> _participants = [];

    public event Action? StateChanged;
    public event Action<string>? ErrorOccurred;
    public event Action? GroupCallPickerRequested;

    public CallStage Stage { get; private set; } = CallStage.Idle;
    public CallInviteDto? IncomingInvite { get; private set; }
    public Guid? ActiveCallId { get; private set; }
    public IReadOnlyCollection<CallParticipantView> Participants => _participants.Values;
    public bool MicOn { get; private set; } = true;
    public bool CameraOn { get; private set; } = true;
    public bool ScreenSharing { get; private set; }
    public bool IsConnected => _hub?.State == HubConnectionState.Connected;
    public int MaxParticipants => _webRtcOptions.Value.MaxParticipants;

    public CallService(
        IAuthService authService,
        IJSRuntime jsRuntime,
        IOptions<WebRtcOptions> webRtcOptions,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CallService> logger)
    {
        _authService = authService;
        _js = jsRuntime;
        _webRtcOptions = webRtcOptions;
        _logger = logger;

        var request = httpContextAccessor.HttpContext?.Request;
        var scheme = request?.Scheme ?? "https";
        var host = request?.Host.ToString() ?? "localhost:7090";
        _hubUrl = $"{scheme}://{host}/hubs/call";
    }

    public async Task EnsureConnectedAsync(ClaimsPrincipal? circuitUser = null)
    {
        if (_hub is not null)
            return;

        _hub = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => circuitUser is not null
                    ? _authService.GetTokenAsync(circuitUser)
                    : _authService.GetTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        RegisterHubHandlers();
        RegisterSignalingHandlers();

        _hub.Reconnecting += _ => { NotifyStateChanged(); return Task.CompletedTask; };
        _hub.Closed += _ => { NotifyStateChanged(); return Task.CompletedTask; };
        _hub.Reconnected += async _ =>
        {
            NotifyStateChanged();
            if (ActiveCallId is not null)
            {
                await RejoinAsync(ActiveCallId.Value);
            }
        };

        try
        {
            await _hub.StartAsync();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR hub connection to {HubUrl}.", _hubUrl);
            await _hub.DisposeAsync();
            _hub = null;
            NotifyStateChanged();
        }
    }

    private void RegisterHubHandlers()
    {
        if (_hub is null) return;

        _hub.On<CallInviteDto>("IncomingCall", invite =>
        {
            // If this tab is already busy, it simply doesn't show the toast — the invite still
            // rings this user's other tabs via their own user_{userId} group membership.
            if (Stage != CallStage.Idle) return;

            IncomingInvite = invite;
            ActiveCallId = invite.CallId;
            Stage = CallStage.Incoming;
            NotifyStateChanged();
        });

        _hub.On<Guid>("IncomingCallHandled", callId =>
        {
            if (Stage == CallStage.Incoming && ActiveCallId == callId)
            {
                _ = EndLocalCallAsync();
            }
        });

        _hub.On<Guid, CallParticipantDto>("CallAccepted", (callId, _) =>
        {
            if (ActiveCallId == callId && Stage == CallStage.Outgoing)
            {
                Stage = CallStage.Connecting;
                NotifyStateChanged();
            }
        });

        _hub.On<Guid, CallParticipantDto>("ParticipantJoined", async (callId, participant) =>
        {
            // The group broadcast includes the joiner's own connection — skip wiring a peer to ourselves.
            if (ActiveCallId != callId || participant.ConnectionId == _hub.ConnectionId) return;

            if (Stage == CallStage.Outgoing) Stage = CallStage.Connecting;

            // We're already established here, so the newcomer is the one who offers to us.
            await ConnectToParticipantAsync(participant, initiateOffer: false);

            if (Stage == CallStage.Connecting) Stage = CallStage.InCall;
            NotifyStateChanged();
        });

        _hub.On<Guid, string>("ParticipantLeft", async (callId, userId) =>
        {
            if (ActiveCallId != callId) return;

            var entry = _participants.Values.FirstOrDefault(p => p.UserId == userId);
            if (entry is null) return;

            await _js.InvokeVoidAsync("webrtcInterop.closePeer", entry.ConnectionId);
            _participants.Remove(entry.ConnectionId);
            NotifyStateChanged();
        });

        _hub.On<Guid, string>("ParticipantReconnected", (_, _) => { /* connectionId unchanged from our side; nothing to reconcile */ });

        _hub.On<Guid, string, MediaStateDto>("ParticipantMediaChanged", (callId, userId, state) =>
        {
            if (ActiveCallId != callId) return;

            var entry = _participants.Values.FirstOrDefault(p => p.UserId == userId);
            if (entry is null) return;

            entry.MicOn = state.MicOn;
            entry.CameraOn = state.CameraOn;
            entry.ScreenSharing = state.ScreenSharing;
            NotifyStateChanged();
        });

        _hub.On<Guid, string>("CallDeclined", (callId, _) =>
        {
            if (ActiveCallId == callId) ErrorOccurred?.Invoke("CallDeclined");
        });

        _hub.On<Guid, string>("CallCancelled", async (callId, _) =>
        {
            if (ActiveCallId == callId) await EndLocalCallAsync();
        });

        _hub.On<Guid, string>("CallUnavailable", (callId, _) =>
        {
            if (ActiveCallId == callId) ErrorOccurred?.Invoke("CallUnavailable");
        });

        _hub.On<Guid, CallEndReason>("CallEnded", async (callId, _) =>
        {
            if (ActiveCallId == callId) await EndLocalCallAsync();
        });

        _hub.On<string>("CallError", message => ErrorOccurred?.Invoke(message));
    }

    public async Task StartCallAsync(List<string> userIds, Guid? conversationId = null)
    {
        if (_hub is null || Stage != CallStage.Idle) return;

        if (!await StartLocalMediaAsync())
            return;

        var result = await _hub.InvokeAsync<StartCallResultDto>("StartCall", userIds, conversationId);
        if (result.StartCallStatus != StartCallStatus.Ringing || result.CallId is null)
        {
            await _js.InvokeVoidAsync("webrtcInterop.closeAll");
            // WP6 maps this status name to a localized CallRes message.
            ErrorOccurred?.Invoke(result.StartCallStatus.ToString());
            return;
        }

        ActiveCallId = result.CallId;
        Stage = CallStage.Outgoing;

        // Joined audio-only (camera unavailable) — tell the others so their tiles show camera-off.
        if (!CameraOn)
            await PushMediaStateAsync();

        NotifyStateChanged();
    }

    public async Task AcceptAsync()
    {
        if (_hub is null || Stage != CallStage.Incoming || ActiveCallId is null) return;

        var callId = ActiveCallId.Value;

        if (!await StartLocalMediaAsync())
            return; // stays Incoming — no auto-decline, user can retry after fixing permissions

        var join = await _hub.InvokeAsync<JoinCallResultDto?>("AcceptCall", callId);
        if (join is null)
        {
            await EndLocalCallAsync();
            return;
        }

        IncomingInvite = null;
        Stage = CallStage.Connecting;

        // Joined audio-only (camera unavailable) — tell the others so their tiles show camera-off.
        if (!CameraOn)
            await PushMediaStateAsync();

        NotifyStateChanged();

        foreach (var participant in join.ExistingParticipants)
        {
            await ConnectToParticipantAsync(participant, initiateOffer: true);
        }

        Stage = CallStage.InCall;
        NotifyStateChanged();
    }

    public async Task DeclineAsync()
    {
        if (_hub is null || Stage != CallStage.Incoming || ActiveCallId is null) return;
        await _hub.InvokeAsync("DeclineCall", ActiveCallId.Value);
        await EndLocalCallAsync();
    }

    public async Task CancelAsync()
    {
        if (_hub is null || Stage != CallStage.Outgoing || ActiveCallId is null) return;
        await _hub.InvokeAsync("CancelCall", ActiveCallId.Value);
        await EndLocalCallAsync();
    }

    public async Task HangUpAsync()
    {
        if (_hub is null || ActiveCallId is null) return;
        if (Stage != CallStage.Connecting && Stage != CallStage.InCall) return;

        await _hub.InvokeAsync("LeaveCall", ActiveCallId.Value);
        await EndLocalCallAsync();
    }

    public async Task InviteAsync(string userId)
    {
        if (_hub is null || ActiveCallId is null) return;
        await _hub.InvokeAsync("InviteToCall", ActiveCallId.Value, userId);
    }

    public async Task ToggleMicAsync()
    {
        MicOn = !MicOn;
        await _js.InvokeVoidAsync("webrtcInterop.setMicEnabled", MicOn);
        await PushMediaStateAsync();
        NotifyStateChanged();
    }

    public async Task ToggleCameraAsync()
    {
        var target = !CameraOn;
        // False when no local video track exists (call joined audio-only) — stay camera-off
        // rather than showing an "on" state that sends nothing.
        var hasVideoTrack = await _js.InvokeAsync<bool>("webrtcInterop.setCameraEnabled", target);
        CameraOn = target && hasVideoTrack;
        await PushMediaStateAsync();
        NotifyStateChanged();
    }

    public async Task ToggleScreenShareAsync()
    {
        if (ScreenSharing)
        {
            await _js.InvokeVoidAsync("webrtcInterop.stopScreenShare", "video-local");
            ScreenSharing = false;
        }
        else
        {
            ScreenSharing = await _js.InvokeAsync<bool>("webrtcInterop.startScreenShare", "video-local");
        }

        await PushMediaStateAsync();
        NotifyStateChanged();
    }

    public async Task<List<ChatUserDto>> GetCallableUsersAsync(string? search = null)
    {
        if (_hub is null) return [];
        return await _hub.InvokeAsync<List<ChatUserDto>>("GetCallableUsers", search);
    }

    public async Task BindVideoAsync(string connectionId, string elementId)
        => await _js.InvokeVoidAsync("webrtcInterop.bindRemoteVideo", connectionId, elementId);

    public async Task BindLocalVideoAsync(string elementId)
        => await _js.InvokeVoidAsync("webrtcInterop.bindLocalVideo", elementId);

    public void RequestGroupCallPicker() => GroupCallPickerRequested?.Invoke();

    private async Task RejoinAsync(Guid callId)
    {
        if (_hub is null) return;

        var join = await _hub.InvokeAsync<JoinCallResultDto?>("RejoinCall", callId);
        if (join is null)
        {
            // Server has no memory of this call (e.g. restarted mid-call) — nothing to resume.
            await EndLocalCallAsync();
            return;
        }

        // Existing WebRTC peer connections are untouched by a SignalR reconnect (media keeps
        // flowing P2P) — only wire up participants who joined while we were disconnected.
        foreach (var participant in join.ExistingParticipants)
        {
            if (_participants.ContainsKey(participant.ConnectionId)) continue;
            await ConnectToParticipantAsync(participant, initiateOffer: true);
        }

        NotifyStateChanged();
    }

    private async Task ConnectToParticipantAsync(CallParticipantDto participant, bool initiateOffer)
    {
        _participants[participant.ConnectionId] = new CallParticipantView
        {
            UserId = participant.UserId,
            DisplayName = participant.DisplayName,
            ConnectionId = participant.ConnectionId,
            MicOn = participant.MicOn,
            CameraOn = participant.CameraOn,
            ScreenSharing = participant.ScreenSharing
        };

        var polite = string.CompareOrdinal(_hub!.ConnectionId, participant.ConnectionId) > 0;
        await _js.InvokeVoidAsync("webrtcInterop.createPeer", participant.ConnectionId, polite);

        if (initiateOffer)
        {
            await _js.InvokeVoidAsync("webrtcInterop.initiateOffer", participant.ConnectionId);
        }

        NotifyStateChanged();
    }

    private async Task PushMediaStateAsync()
    {
        if (_hub is null || ActiveCallId is null) return;
        await _hub.InvokeAsync("UpdateMediaState", ActiveCallId.Value, new MediaStateDto(MicOn, CameraOn, ScreenSharing));
    }

    private async Task EndLocalCallAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("webrtcInterop.closeAll");
        }
        catch (JSDisconnectedException) { }

        _participants.Clear();
        ActiveCallId = null;
        IncomingInvite = null;
        Stage = CallStage.Idle;
        MicOn = true;
        CameraOn = true;
        ScreenSharing = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }
}
