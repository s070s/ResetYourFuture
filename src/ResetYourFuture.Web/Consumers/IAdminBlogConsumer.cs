using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for the admin blog management API.
/// </summary>
public interface IAdminBlogConsumer
{
    Task<PagedResult<AdminBlogArticleDto>?> GetArticlesAsync( int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default );
    Task<AdminBlogArticleDto?> GetArticleAsync( Guid id, CancellationToken ct = default );
    Task<AdminBlogArticleDto?> CreateArticleAsync( SaveBlogArticleRequest request, CancellationToken ct = default );
    Task<AdminBlogArticleDto?> UpdateArticleAsync( Guid id, SaveBlogArticleRequest request, CancellationToken ct = default );
    Task<bool> PublishArticleAsync( Guid id, CancellationToken ct = default );
    Task<bool> UnpublishArticleAsync( Guid id, CancellationToken ct = default );
    Task<bool> DeleteArticleAsync( Guid id, CancellationToken ct = default );
    Task<string?> UploadCoverImageAsync( Guid id, IBrowserFile file, CancellationToken ct = default );
}
