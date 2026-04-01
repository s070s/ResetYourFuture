using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin blog management API.
/// </summary>
public class AdminBlogConsumer : IAdminBlogConsumer
{
    private readonly HttpClient _http;

    public AdminBlogConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<AdminBlogArticleDto>?> GetArticlesAsync(
        int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default )
    {
        var url = $"api/admin/blog?page={page}&pageSize={pageSize}";
        if ( !string.IsNullOrWhiteSpace( search ) )
            url += $"&search={Uri.EscapeDataString( search )}";

        var response = await _http.GetAsync( url, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<AdminBlogArticleDto>>( ct )
            : null;
    }

    public async Task<AdminBlogArticleDto?> GetArticleAsync(
        Guid id, CancellationToken ct = default )
    {
        var response = await _http.GetAsync( $"api/admin/blog/{id}", ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminBlogArticleDto>( ct )
            : null;
    }

    public async Task<AdminBlogArticleDto?> CreateArticleAsync(
        SaveBlogArticleRequest request, CancellationToken ct = default )
    {
        var response = await _http.PostAsJsonAsync( "api/admin/blog", request, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminBlogArticleDto>( ct )
            : null;
    }

    public async Task<AdminBlogArticleDto?> UpdateArticleAsync(
        Guid id, SaveBlogArticleRequest request, CancellationToken ct = default )
    {
        var response = await _http.PutAsJsonAsync( $"api/admin/blog/{id}", request, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminBlogArticleDto>( ct )
            : null;
    }

    public async Task<bool> PublishArticleAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.PostAsync( $"api/admin/blog/{id}/publish", null, ct );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnpublishArticleAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.PostAsync( $"api/admin/blog/{id}/unpublish", null, ct );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteArticleAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.DeleteAsync( $"api/admin/blog/{id}", ct );
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> UploadCoverImageAsync( Guid id, IBrowserFile file, CancellationToken ct = default )
    {
        const long maxSize = 5 * 1024 * 1024; // 5 MB
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent, "file", file.Name );

        var response = await _http.PostAsync( $"api/admin/blog/{id}/upload/cover", content, ct );
        if ( !response.IsSuccessStatusCode ) return null;

        var result = await response.Content.ReadFromJsonAsync<CoverUploadResult>( ct );
        return result?.CoverImageUrl;
    }

    private record CoverUploadResult( string CoverImageUrl );
}
