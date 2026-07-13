using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Data-layer CRUD for notifications. Framework-agnostic — the SignalR push half of
/// dispatching lives in <see cref="INotificationDispatcher"/> (Web layer), which calls
/// <see cref="CreateAsync"/> then pushes the returned entity over the wire.
/// </summary>
public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetPagedAsync(
        string userId, int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    Task<Notification> CreateAsync(
        string userId, NotificationType type, string titleKey, IReadOnlyList<string>? bodyArgs, string? linkUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Returns false if the notification doesn't exist or belongs to a different user.</summary>
    Task<bool> MarkReadAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the number of rows marked read.</summary>
    Task<int> MarkAllReadAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes read notifications older than the cutoff. Returns the number of rows removed.</summary>
    Task<int> PruneOldAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
}
