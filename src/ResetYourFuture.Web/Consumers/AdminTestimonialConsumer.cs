using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin testimonials management API.
/// </summary>
public class AdminTestimonialConsumer : IAdminTestimonialConsumer
{
    private readonly HttpClient _http;

    public AdminTestimonialConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<AdminTestimonialDto>?> GetAllAsync(
        int page = 1, int pageSize = 10, CancellationToken ct = default )
    {
        var response = await _http.GetAsync( $"api/admin/testimonials?page={page}&pageSize={pageSize}", ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<AdminTestimonialDto>>( ct )
            : null;
    }

    public async Task<AdminTestimonialDto?> GetByIdAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.GetAsync( $"api/admin/testimonials/{id}", ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminTestimonialDto>( ct )
            : null;
    }

    public async Task<AdminTestimonialDto?> CreateAsync(
        SaveTestimonialRequest request, CancellationToken ct = default )
    {
        var response = await _http.PostAsJsonAsync( "api/admin/testimonials", request, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminTestimonialDto>( ct )
            : null;
    }

    public async Task<AdminTestimonialDto?> UpdateAsync(
        Guid id, SaveTestimonialRequest request, CancellationToken ct = default )
    {
        var response = await _http.PutAsJsonAsync( $"api/admin/testimonials/{id}", request, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminTestimonialDto>( ct )
            : null;
    }

    public async Task<AdminTestimonialDto?> ToggleActiveAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.PostAsync( $"api/admin/testimonials/{id}/toggle-active", null, ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminTestimonialDto>( ct )
            : null;
    }

    public async Task<bool> MoveUpAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.PostAsync( $"api/admin/testimonials/{id}/move-up", null, ct );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MoveDownAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.PostAsync( $"api/admin/testimonials/{id}/move-down", null, ct );
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> UploadAvatarAsync(
        Guid id, IBrowserFile file, CancellationToken ct = default )
    {
        const long maxSize = 5 * 1024 * 1024; // 5 MB
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent, "file", file.Name );

        var response = await _http.PostAsync( $"api/admin/testimonials/{id}/upload/avatar", content, ct );
        if ( !response.IsSuccessStatusCode )
            return null;

        var result = await response.Content.ReadFromJsonAsync<AvatarUploadResult>( ct );
        return result?.AvatarPath;
    }

    public async Task<bool> RemoveAvatarAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.DeleteAsync( $"api/admin/testimonials/{id}/avatar", ct );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync( Guid id, CancellationToken ct = default )
    {
        var response = await _http.DeleteAsync( $"api/admin/testimonials/{id}", ct );
        return response.IsSuccessStatusCode;
    }

    private record AvatarUploadResult( string AvatarPath );
}
