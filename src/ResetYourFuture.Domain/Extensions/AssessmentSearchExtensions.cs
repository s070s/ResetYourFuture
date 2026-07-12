using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class AssessmentSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to an admin assessment-definition query.
    /// Same pattern as <see cref="UserSearchExtensions"/>: a switch expression keeps
    /// EF Core SQL translation intact, and every branch ends with a stable
    /// .ThenBy(Id) tie-breaker so ordering is deterministic across pages.
    /// Unknown keys fall through to the pre-sorting default: CreatedAt descending.
    /// </summary>
    public static IQueryable<AssessmentDefinition> ApplySort(
        this IQueryable<AssessmentDefinition> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("title", "desc") => query.OrderByDescending(a => a.TitleEn),
            ("title", _) => query.OrderBy(a => a.TitleEn),
            ("key", "desc") => query.OrderByDescending(a => a.Key),
            ("key", _) => query.OrderBy(a => a.Key),
            ("category", "desc") => query.OrderByDescending(a => a.Category != null ? a.Category.NameEn : null),
            ("category", _) => query.OrderBy(a => a.Category != null ? a.Category.NameEn : null),
            ("tier", "desc") => query.OrderByDescending(a => a.RequiredTier),
            ("tier", _) => query.OrderBy(a => a.RequiredTier),
            ("status", "desc") => query.OrderByDescending(a => a.IsPublished),
            ("status", _) => query.OrderBy(a => a.IsPublished),
            ("submissions", "desc") => query.OrderByDescending(a => a.Submissions.Count),
            ("submissions", _) => query.OrderBy(a => a.Submissions.Count),
            ("createdat", "asc") => query.OrderBy(a => a.CreatedAt),
            _ => query.OrderByDescending(a => a.CreatedAt),
        };
        return ordered.ThenBy(a => a.Id);
    }
}
