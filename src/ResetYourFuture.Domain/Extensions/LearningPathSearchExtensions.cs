using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class LearningPathSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to the admin learning-paths list. Same pattern as
    /// <see cref="CourseReviewSearchExtensions"/>: switch expression, EF-translatable,
    /// always ends with a stable .ThenBy(Id) tie-breaker. Default matches the catalog's
    /// own ordering (DisplayOrder ascending).
    /// </summary>
    public static IQueryable<LearningPath> ApplySort(
        this IQueryable<LearningPath> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("titleen", "desc") => query.OrderByDescending(p => p.TitleEn),
            ("titleen", _) => query.OrderBy(p => p.TitleEn),
            ("ispublished", "desc") => query.OrderByDescending(p => p.IsPublished),
            ("ispublished", _) => query.OrderBy(p => p.IsPublished),
            ("createdat", "desc") => query.OrderByDescending(p => p.CreatedAt),
            ("createdat", _) => query.OrderBy(p => p.CreatedAt),
            ("displayorder", "desc") => query.OrderByDescending(p => p.DisplayOrder),
            _ => query.OrderBy(p => p.DisplayOrder),
        };
        return ordered.ThenBy(p => p.Id);
    }
}
