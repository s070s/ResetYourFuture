using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin blog management API.
/// </summary>
public class AdminBlogConsumer( HttpClient http ) : ApiClientBase( http ), IAdminBlogConsumer
{
    public Task<PagedResult<AdminBlogArticleDto>?> GetArticlesAsync(
        int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default )
    {
        var url = $"api/admin/blog?page={page}&pageSize={pageSize}";
        if ( !string.IsNullOrWhiteSpace( search ) )
            url += $"&search={Uri.EscapeDataString( search )}";
        return GetAsync<PagedResult<AdminBlogArticleDto>>( url, ct );
    }

    public Task<AdminBlogArticleDto?> GetArticleAsync( Guid id, CancellationToken ct = default )
        => GetAsync<AdminBlogArticleDto>( $"api/admin/blog/{id}", ct );

    public Task<AdminBlogArticleDto?> CreateArticleAsync(
        SaveBlogArticleRequest request, CancellationToken ct = default )
        => PostJsonAsync<SaveBlogArticleRequest, AdminBlogArticleDto>( "api/admin/blog", request, ct );

    public Task<AdminBlogArticleDto?> UpdateArticleAsync(
        Guid id, SaveBlogArticleRequest request, CancellationToken ct = default )
        => PutJsonAsync<SaveBlogArticleRequest, AdminBlogArticleDto>( $"api/admin/blog/{id}", request, ct );

    public Task<bool> PublishArticleAsync( Guid id, CancellationToken ct = default )
        => ActionAsync( $"api/admin/blog/{id}/publish", ct );

    public Task<bool> UnpublishArticleAsync( Guid id, CancellationToken ct = default )
        => ActionAsync( $"api/admin/blog/{id}/unpublish", ct );

    public Task<bool> DeleteArticleAsync( Guid id, CancellationToken ct = default )
        => DeleteAsync( $"api/admin/blog/{id}", ct );

    public async Task<string?> UploadCoverImageAsync( Guid id, IBrowserFile file, CancellationToken ct = default )
    {
        const long maxSize = 5 * 1024 * 1024;
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent, "file", file.Name );

        var result = await PostFormAsync<CoverUploadResult>( $"api/admin/blog/{id}/upload/cover", content, ct );
        return result?.CoverImageUrl;
    }

    private record CoverUploadResult( string CoverImageUrl );
}
