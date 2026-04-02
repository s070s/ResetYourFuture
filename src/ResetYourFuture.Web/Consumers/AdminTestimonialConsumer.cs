using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin testimonials management API.
/// </summary>
public class AdminTestimonialConsumer( HttpClient http ) : ApiClientBase( http ), IAdminTestimonialConsumer
{
    public Task<PagedResult<AdminTestimonialDto>?> GetAllAsync(
        int page = 1, int pageSize = 10, CancellationToken ct = default )
        => GetAsync<PagedResult<AdminTestimonialDto>>( $"api/admin/testimonials?page={page}&pageSize={pageSize}", ct );

    public Task<AdminTestimonialDto?> GetByIdAsync( Guid id, CancellationToken ct = default )
        => GetAsync<AdminTestimonialDto>( $"api/admin/testimonials/{id}", ct );

    public Task<AdminTestimonialDto?> CreateAsync(
        SaveTestimonialRequest request, CancellationToken ct = default )
        => PostJsonAsync<SaveTestimonialRequest, AdminTestimonialDto>( "api/admin/testimonials", request, ct );

    public Task<AdminTestimonialDto?> UpdateAsync(
        Guid id, SaveTestimonialRequest request, CancellationToken ct = default )
        => PutJsonAsync<SaveTestimonialRequest, AdminTestimonialDto>( $"api/admin/testimonials/{id}", request, ct );

    public Task<AdminTestimonialDto?> ToggleActiveAsync( Guid id, CancellationToken ct = default )
        => PostAsync<AdminTestimonialDto>( $"api/admin/testimonials/{id}/toggle-active", ct );

    public Task<bool> MoveUpAsync( Guid id, CancellationToken ct = default )
        => ActionAsync( $"api/admin/testimonials/{id}/move-up", ct );

    public Task<bool> MoveDownAsync( Guid id, CancellationToken ct = default )
        => ActionAsync( $"api/admin/testimonials/{id}/move-down", ct );

    public async Task<string?> UploadAvatarAsync(
        Guid id, IBrowserFile file, CancellationToken ct = default )
    {
        const long maxSize = 5 * 1024 * 1024;
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent, "file", file.Name );

        var result = await PostFormAsync<AvatarUploadResult>( $"api/admin/testimonials/{id}/upload/avatar", content, ct );
        return result?.AvatarPath;
    }

    public Task<bool> RemoveAvatarAsync( Guid id, CancellationToken ct = default )
        => DeleteAsync( $"api/admin/testimonials/{id}/avatar", ct );

    public Task<bool> DeleteAsync( Guid id, CancellationToken ct = default )
        => DeleteAsync( $"api/admin/testimonials/{id}", ct );

    private record AvatarUploadResult( string AvatarPath );
}
