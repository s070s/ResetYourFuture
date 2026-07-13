using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ResetYourFuture.Web.Services;

namespace ResetYourFuture.Web.Hubs;

/// <summary>
/// Lightweight push channel for notifications: connects globally (mounted once in
/// MainLayout for every authenticated user, like <c>CallHub</c>), joins the same
/// <c>user_{userId}</c> group convention as the other hubs, and has no client-callable
/// methods — the server only ever pushes to it, via <c>NotificationDispatcher</c>'s
/// <c>IHubContext&lt;NotificationHub&gt;</c>.
/// </summary>
[Authorize]
public class NotificationHub(NotificationConnectionTracker tracker) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            tracker.MarkConnected(userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            tracker.MarkDisconnected(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
