using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class CategorySearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to an admin category query.
    /// Same pattern as <see cref="UserSearchExtensions"/>: a switch expression keeps
    /// EF Core SQL translation intact, and every branch ends with a stable
    /// .ThenBy(Id) tie-breaker so ordering is deterministic across pages.
    /// Count keys exclude soft-deleted children, matching the displayed counts.
    /// Unknown keys fall through to the pre-sorting default: NameEn ascending.
    /// </summary>
    public static IQueryable<Category> ApplySort(
        this IQueryable<Category> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("nameel", "desc") => query.OrderByDescending(c => c.NameEl),
            ("nameel", _) => query.OrderBy(c => c.NameEl),
            ("coursecount", "desc") => query.OrderByDescending(c => c.Courses.Count(x => !x.IsDeleted)),
            ("coursecount", _) => query.OrderBy(c => c.Courses.Count(x => !x.IsDeleted)),
            ("assessmentcount", "desc") => query.OrderByDescending(c => c.AssessmentDefinitions.Count(x => !x.IsDeleted)),
            ("assessmentcount", _) => query.OrderBy(c => c.AssessmentDefinitions.Count(x => !x.IsDeleted)),
            ("createdat", "desc") => query.OrderByDescending(c => c.CreatedAt),
            ("createdat", _) => query.OrderBy(c => c.CreatedAt),
            ("nameen", "desc") => query.OrderByDescending(c => c.NameEn),
            _ => query.OrderBy(c => c.NameEn),
        };
        return ordered.ThenBy(c => c.Id);
    }
}
