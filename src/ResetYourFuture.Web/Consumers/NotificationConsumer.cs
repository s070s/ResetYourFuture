using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the notification inbox API.
/// </summary>
public class NotificationConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider)
    : ApiClientBase(http, tokenProvider), INotificationConsumer
{
    public Task<PagedResult<NotificationDto>?> GetNotificationsAsync(int page = 1, int pageSize = 10, string sortBy = "createdat", string sortDir = "desc")
        => GetAsync<PagedResult<NotificationDto>>($"api/notifications?page={page}&pageSize={pageSize}&sortBy={sortBy}&sortDir={sortDir}");

    public Task<NotificationSummaryDto?> GetUnreadCountAsync()
        => GetAsync<NotificationSummaryDto>("api/notifications/unread-count");

    public Task<bool> MarkReadAsync(Guid id)
        => ActionAsync($"api/notifications/{id}/read");

    public Task<bool> MarkAllReadAsync()
        => ActionAsync("api/notifications/read-all");
}
