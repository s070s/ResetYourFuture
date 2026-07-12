using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Domain.Extensions;

public static class BlogArticleSearchExtensions
{
    /// <summary>
    /// Applies server-side sorting to an admin blog-article query.
    /// Same pattern as <see cref="UserSearchExtensions"/>: a switch expression keeps
    /// EF Core SQL translation intact, and every branch ends with a stable
    /// .ThenBy(Id) tie-breaker so ordering is deterministic across pages.
    /// Unknown keys fall through to the pre-sorting default: CreatedAt descending.
    /// </summary>
    public static IQueryable<BlogArticle> ApplySort(
        this IQueryable<BlogArticle> query, string? sortBy, string? sortDir)
    {
        var ordered = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("title", "desc") => query.OrderByDescending(a => a.TitleEn),
            ("title", _) => query.OrderBy(a => a.TitleEn),
            ("slug", "desc") => query.OrderByDescending(a => a.Slug),
            ("slug", _) => query.OrderBy(a => a.Slug),
            ("author", "desc") => query.OrderByDescending(a => a.AuthorName),
            ("author", _) => query.OrderBy(a => a.AuthorName),
            ("status", "desc") => query.OrderByDescending(a => a.IsPublished),
            ("status", _) => query.OrderBy(a => a.IsPublished),
            // Nullable PublishedAt: drafts have NULL, which sorts before any date.
            ("publishedat", "desc") => query.OrderByDescending(a => a.PublishedAt),
            ("publishedat", _) => query.OrderBy(a => a.PublishedAt),
            ("createdat", "asc") => query.OrderBy(a => a.CreatedAt),
            _ => query.OrderByDescending(a => a.CreatedAt),
        };
        return ordered.ThenBy(a => a.Id);
    }
}
