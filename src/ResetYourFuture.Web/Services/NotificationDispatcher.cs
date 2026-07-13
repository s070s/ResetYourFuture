using Microsoft.AspNetCore.SignalR;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Web.Hubs;

namespace ResetYourFuture.Web.Services;

/// <inheritdoc cref="INotificationDispatcher"/>
public class NotificationDispatcher(
    INotificationService notifications,
    IHubContext<NotificationHub> hub) : INotificationDispatcher
{
    public async Task DispatchAsync(
        string userId, NotificationType type, string titleKey, IReadOnlyList<string>? bodyArgs, string? linkUrl,
        CancellationToken cancellationToken = default)
    {
        var notification = await notifications.CreateAsync(userId, type, titleKey, bodyArgs, linkUrl, cancellationToken);

        var dto = notification.ToDto();

        // No-ops silently if the user has no live connection — the row is already persisted,
        // so it shows up on their next login/reconnect via the normal paged fetch.
        await hub.Clients.Group($"user_{userId}").SendAsync("NotificationReceived", dto, cancellationToken);
    }
}
