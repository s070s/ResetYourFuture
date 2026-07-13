using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class NotificationSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to a notification query. Same pattern as
    /// <see cref="UserSearchExtensions"/>: a switch expression keeps EF Core SQL translation
    /// intact, and every branch ends with a stable .ThenBy(Id) tie-breaker. Unknown keys fall
    /// through to the default: CreatedAt descending (newest first).
    /// </summary>
    public static IQueryable<Notification> ApplySort(
        this IQueryable<Notification> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("isread", "desc") => query.OrderByDescending(n => n.IsRead),
            ("isread", _) => query.OrderBy(n => n.IsRead),
            ("createdat", "asc") => query.OrderBy(n => n.CreatedAt),
            _ => query.OrderByDescending(n => n.CreatedAt),
        };
        return ordered.ThenBy(n => n.Id);
    }
}
