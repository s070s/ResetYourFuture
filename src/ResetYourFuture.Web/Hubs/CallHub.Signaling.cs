using Microsoft.AspNetCore.SignalR;

namespace ResetYourFuture.Web.Hubs;

/// <summary>
/// WebRTC offer/answer/ICE-candidate relay methods for <see cref="CallHub"/>. The server never
/// inspects SDP/ICE payloads — it only validates that both the caller and the target connection
/// are members of the given call before relaying, so a rogue client can't probe/inject signaling
/// into a call it isn't part of.
/// </summary>
public partial class CallHub
{
    public async Task SendOffer(Guid callId, string targetConnectionId, object payload)
        => await Relay(callId, targetConnectionId, "ReceiveOffer", payload);

    public async Task SendAnswer(Guid callId, string targetConnectionId, object payload)
        => await Relay(callId, targetConnectionId, "ReceiveAnswer", payload);

    public async Task SendIceCandidate(Guid callId, string targetConnectionId, object payload)
        => await Relay(callId, targetConnectionId, "ReceiveIceCandidate", payload);

    private async Task Relay(Guid callId, string targetConnectionId, string method, object payload)
    {
        if (!_registry.IsMemberConnection(callId, Context.ConnectionId) ||
            !_registry.IsMemberConnection(callId, targetConnectionId))
        {
            _logger.LogWarning(
                "Call: Rejected {Method} relay for call {CallId} — caller or target not a member.",
                method, callId);
            await Clients.Caller.SendAsync("CallError", "You are not an active member of this call.");
            return;
        }

        await Clients.Client(targetConnectionId).SendAsync(method, Context.ConnectionId, payload);
    }
}
