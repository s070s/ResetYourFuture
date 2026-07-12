using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class CourseSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to an admin course query.
    /// Same pattern as <see cref="UserSearchExtensions"/>: a switch expression keeps
    /// EF Core SQL translation intact, and every branch ends with a stable
    /// .ThenBy(Id) tie-breaker so ordering is deterministic across pages.
    /// Unknown keys fall through to the pre-sorting default: CreatedAt descending.
    /// </summary>
    public static IQueryable<Course> ApplySort(
        this IQueryable<Course> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("title", "desc") => query.OrderByDescending(c => c.TitleEn),
            ("title", _) => query.OrderBy(c => c.TitleEn),
            // Conditional (not null-forgiving) access so the expression is also safe
            // to evaluate in-memory; uncategorized courses sort together as null.
            ("category", "desc") => query.OrderByDescending(c => c.Category != null ? c.Category.NameEn : null),
            ("category", _) => query.OrderBy(c => c.Category != null ? c.Category.NameEn : null),
            ("tier", "desc") => query.OrderByDescending(c => c.RequiredTier),
            ("tier", _) => query.OrderBy(c => c.RequiredTier),
            ("status", "desc") => query.OrderByDescending(c => c.IsPublished),
            ("status", _) => query.OrderBy(c => c.IsPublished),
            // Translates to a correlated COUNT subquery — fine at this scale.
            ("enrollments", "desc") => query.OrderByDescending(c => c.Enrollments.Count),
            ("enrollments", _) => query.OrderBy(c => c.Enrollments.Count),
            ("createdat", "asc") => query.OrderBy(c => c.CreatedAt),
            _ => query.OrderByDescending(c => c.CreatedAt),
        };
        return ordered.ThenBy(c => c.Id);
    }
}
