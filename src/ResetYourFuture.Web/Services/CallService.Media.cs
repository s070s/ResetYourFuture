using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Text.Json;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// JS interop half of <see cref="CallService"/>: bootstraps <c>window.webrtcInterop</c>, relays
/// SignalR offer/answer/ICE messages into it, and exposes the <see cref="JSInvokableAttribute"/>
/// callbacks webrtc-interop.js calls back into.
/// </summary>
public partial class CallService
{
    private DotNetObjectReference<CallService>? _dotNetRef;

    private void RegisterSignalingHandlers()
    {
        if (_hub is null) return;

        _hub.On<string, JsonElement>("ReceiveOffer", async (fromConnectionId, payload) => await HandleRemoteDescriptionAsync(fromConnectionId, payload));
        _hub.On<string, JsonElement>("ReceiveAnswer", async (fromConnectionId, payload) => await HandleRemoteDescriptionAsync(fromConnectionId, payload));

        _hub.On<string, JsonElement>("ReceiveIceCandidate", async (fromConnectionId, payload) =>
        {
            var candidateJson = payload.GetString();
            if (candidateJson is not null)
            {
                await _js.InvokeVoidAsync("webrtcInterop.addIceCandidate", fromConnectionId, candidateJson);
            }
        });
    }

    private async Task HandleRemoteDescriptionAsync(string fromConnectionId, JsonElement payload)
    {
        var type = payload.GetProperty("type").GetString();
        var sdp = payload.GetProperty("sdp").GetString();
        if (type is null || sdp is null) return;

        await _js.InvokeVoidAsync("webrtcInterop.setRemoteDescription", fromConnectionId, type, sdp);
    }

    private async Task EnsureJsInitializedAsync()
    {
        if (_dotNetRef is not null) return;

        _dotNetRef = DotNetObjectReference.Create(this);
        var iceServers = _webRtcOptions.Value.IceServers
            .Select(s => new { urls = s.Urls, username = s.Username, credential = s.Credential })
            .ToList();

        await _js.InvokeVoidAsync("webrtcInterop.init", _dotNetRef, iceServers);
    }

    private async Task<bool> StartLocalMediaAsync()
    {
        await EnsureJsInitializedAsync();

        var ok = await _js.InvokeAsync<bool>("webrtcInterop.startLocalMedia", true, true);
        if (ok)
        {
            MicOn = true;
            CameraOn = true;
            ScreenSharing = false;
        }
        return ok;
    }

    [JSInvokable]
    public async Task OnLocalDescription(string peerKey, string type, string sdp)
    {
        if (_hub is null || ActiveCallId is null) return;

        var payload = new Dictionary<string, string> { ["type"] = type, ["sdp"] = sdp };
        var method = type == "offer" ? "SendOffer" : "SendAnswer";
        await _hub.InvokeAsync(method, ActiveCallId.Value, peerKey, payload);
    }

    [JSInvokable]
    public async Task OnIceCandidate(string peerKey, string candidateJson)
    {
        if (_hub is null || ActiveCallId is null) return;
        await _hub.InvokeAsync("SendIceCandidate", ActiveCallId.Value, peerKey, candidateJson);
    }

    [JSInvokable]
    public void OnPeerConnectionState(string peerKey, string state)
    {
        if (_participants.TryGetValue(peerKey, out var participant))
        {
            participant.PeerConnectionState = state;
            NotifyStateChanged();
        }

        if (state == "failed")
        {
            // webrtc-interop.js guards this to a single attempt per peer.
            _ = _js.InvokeVoidAsync("webrtcInterop.restartIce", peerKey);
        }
    }

    [JSInvokable]
    public void OnRemoteStreamChanged(string peerKey) => NotifyStateChanged();

    [JSInvokable]
    public async Task OnScreenShareEnded()
    {
        ScreenSharing = false;
        await PushMediaStateAsync();
        NotifyStateChanged();
    }

    [JSInvokable]
    public void OnMediaError(string context, string? peerKey, string message)
    {
        _logger.LogWarning("CallService: JS interop error in {Context} (peer={PeerKey}): {Message}", context, peerKey, message);

        // Browser getUserMedia failures are virtually always a permissions problem from the
        // user's perspective, regardless of the exact DOMException subtype — surface a clean,
        // localizable key instead of the raw (English-only) browser error text.
        ErrorOccurred?.Invoke(context == "startLocalMedia" ? "MediaPermissionDenied" : message);
    }
}
