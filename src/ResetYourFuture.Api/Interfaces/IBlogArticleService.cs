using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Api.Interfaces;

/// <summary>
/// Service contract for blog article operations.
/// </summary>
public interface IBlogArticleService
{
    /// <summary>Returns the latest N published articles ordered by PublishedAt desc.</summary>
    Task<IReadOnlyList<BlogArticleSummaryDto>> GetPublishedSummariesAsync( int count, string lang = "en", CancellationToken cancellationToken = default );

    /// <summary>Returns a single published article by slug, or null if not found.</summary>
    Task<BlogArticleDto?> GetPublishedBySlugAsync( string slug, string lang = "en", CancellationToken cancellationToken = default );

    /// <summary>Returns all articles paginated for the admin list (both language variants).</summary>
    Task<PagedResult<AdminBlogArticleDto>> GetAllForAdminAsync( int page, int pageSize, string? search, CancellationToken cancellationToken = default );

    /// <summary>Returns a single article by id for the admin editor, or null if not found.</summary>
    Task<AdminBlogArticleDto?> GetByIdForAdminAsync( Guid id, CancellationToken cancellationToken = default );

    /// <summary>Creates a new article. Returns null if the slug is already in use.</summary>
    Task<AdminBlogArticleDto?> CreateAsync( SaveBlogArticleRequest request, CancellationToken cancellationToken = default );

    /// <summary>Updates an existing article. Returns null if not found or slug conflict.</summary>
    Task<AdminBlogArticleDto?> UpdateAsync( Guid id, SaveBlogArticleRequest request, CancellationToken cancellationToken = default );

    /// <summary>Publishes an article. Returns false if not found.</summary>
    Task<bool> PublishAsync( Guid id, CancellationToken cancellationToken = default );

    /// <summary>Unpublishes an article. Returns false if not found.</summary>
    Task<bool> UnpublishAsync( Guid id, CancellationToken cancellationToken = default );

    /// <summary>Hard-deletes an article. Returns false if not found.</summary>
    Task<bool> DeleteAsync( Guid id, CancellationToken cancellationToken = default );
}
