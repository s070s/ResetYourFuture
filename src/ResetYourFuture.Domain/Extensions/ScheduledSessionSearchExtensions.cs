using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class ScheduledSessionSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to the admin sessions list. Same pattern as
    /// <see cref="LearningPathSearchExtensions"/>: switch expression, EF-translatable, always
    /// ends with a stable .ThenBy(Id) tie-breaker. Default is soonest-first (StartsAtUtc asc).
    /// </summary>
    public static IQueryable<ScheduledSession> ApplySort(
        this IQueryable<ScheduledSession> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("titleen", "desc") => query.OrderByDescending(s => s.TitleEn),
            ("titleen", _) => query.OrderBy(s => s.TitleEn),
            ("status", "desc") => query.OrderByDescending(s => s.Status),
            ("status", _) => query.OrderBy(s => s.Status),
            ("startsatutc", "desc") => query.OrderByDescending(s => s.StartsAtUtc),
            _ => query.OrderBy(s => s.StartsAtUtc),
        };
        return ordered.ThenBy(s => s.Id);
    }
}
