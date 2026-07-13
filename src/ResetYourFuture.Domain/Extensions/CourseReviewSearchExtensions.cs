using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class CourseReviewSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to the admin review-moderation query. Same pattern as
    /// <see cref="UserSearchExtensions"/>: a switch expression keeps EF Core SQL translation
    /// intact, and every branch ends with a stable .ThenBy(Id) tie-breaker. Unknown keys fall
    /// through to the default: CreatedAt descending (newest/pending-first for the queue).
    /// </summary>
    public static IQueryable<CourseReview> ApplySort(
        this IQueryable<CourseReview> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("rating", "desc") => query.OrderByDescending(r => r.Rating),
            ("rating", _) => query.OrderBy(r => r.Rating),
            ("status", "desc") => query.OrderByDescending(r => r.Status),
            ("status", _) => query.OrderBy(r => r.Status),
            ("createdat", "asc") => query.OrderBy(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt),
        };
        return ordered.ThenBy(r => r.Id);
    }
}
