using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class TestimonialSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to an admin testimonial query.
    /// Same pattern as <see cref="UserSearchExtensions"/>: a switch expression keeps
    /// EF Core SQL translation intact, and every branch ends with a stable
    /// .ThenBy(Id) tie-breaker so ordering is deterministic across pages.
    /// Unknown keys fall through to the pre-sorting default:
    /// DisplayOrder ascending, then CreatedAt (the manual curation order).
    /// </summary>
    public static IQueryable<Testimonial> ApplySort(
        this IQueryable<Testimonial> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("name", "desc") => query.OrderByDescending(t => t.FullName),
            ("name", _) => query.OrderBy(t => t.FullName),
            ("status", "desc") => query.OrderByDescending(t => t.IsActive),
            ("status", _) => query.OrderBy(t => t.IsActive),
            ("createdat", "desc") => query.OrderByDescending(t => t.CreatedAt),
            ("createdat", _) => query.OrderBy(t => t.CreatedAt),
            ("displayorder", "desc") => query.OrderByDescending(t => t.DisplayOrder).ThenByDescending(t => t.CreatedAt),
            _ => query.OrderBy(t => t.DisplayOrder).ThenBy(t => t.CreatedAt),
        };
        return ordered.ThenBy(t => t.Id);
    }
}
