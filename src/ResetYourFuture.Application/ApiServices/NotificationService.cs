using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Extensions;

namespace ResetYourFuture.Application.ApiServices;

/// <inheritdoc cref="INotificationService"/>
public class NotificationService(IApplicationDbContext db) : INotificationService
{
    public async Task<PagedResult<NotificationDto>> GetPagedAsync(
        string userId, int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        // Materialize entities first — ToDto's JSON deserialization can't translate to SQL.
        var entities = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>(entities.Select(ToDto).ToList(), totalCount, page, pageSize, sortBy, sortDir);
    }

    public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
        db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task<Notification> CreateAsync(
        string userId, NotificationType type, string titleKey, IReadOnlyList<string>? bodyArgs, string? linkUrl,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            TitleKey = titleKey,
            BodyArgsJson = bodyArgs is { Count: > 0 } ? JsonSerializer.Serialize(bodyArgs) : null,
            LinkUrl = linkUrl
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public async Task<bool> MarkReadAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        // Tracked mutation + SaveChanges (not ExecuteUpdateAsync) so this runs on every provider
        // this app targets, including EF Core InMemory (used by the Web.Tests factory).
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (notification is null)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(cancellationToken);
        foreach (var notification in unread)
            notification.IsRead = true;

        if (unread.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return unread.Count;
    }

    public async Task<int> PruneOldAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - olderThan;
        var stale = await db.Notifications.Where(n => n.IsRead && n.CreatedAt < cutoff).ToListAsync(cancellationToken);

        foreach (var notification in stale)
            db.Notifications.Remove(notification);

        if (stale.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return stale.Count;
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.Id,
        n.Type.ToString(),
        n.TitleKey,
        n.BodyArgsJson == null ? [] : JsonSerializer.Deserialize<List<string>>(n.BodyArgsJson) ?? [],
        n.LinkUrl,
        n.IsRead,
        n.CreatedAt);
}
