using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for the notification inbox REST endpoints.
/// </summary>
public interface INotificationConsumer
{
    Task<PagedResult<NotificationDto>?> GetNotificationsAsync(int page = 1, int pageSize = 10, string sortBy = "createdat", string sortDir = "desc");
    Task<NotificationSummaryDto?> GetUnreadCountAsync();
    Task<bool> MarkReadAsync(Guid id);
    Task<bool> MarkAllReadAsync();
}
