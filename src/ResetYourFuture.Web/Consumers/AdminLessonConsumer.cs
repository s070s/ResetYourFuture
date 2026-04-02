using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin lesson management API.
/// </summary>
public class AdminLessonConsumer( HttpClient http ) : ApiClientBase( http ), IAdminLessonConsumer
{
    public async Task<List<AdminLessonDto>> GetLessonsByModuleAsync( Guid moduleId )
        => await GetAsync<List<AdminLessonDto>>( $"api/admin/lessons/module/{moduleId}" ) ?? [];

    public Task<AdminLessonDto?> CreateLessonAsync( SaveLessonRequest request )
        => PostJsonAsync<SaveLessonRequest, AdminLessonDto>( "api/admin/lessons", request );

    public Task<AdminLessonDto?> UpdateLessonAsync( Guid id, SaveLessonRequest request )
        => PutJsonAsync<SaveLessonRequest, AdminLessonDto>( $"api/admin/lessons/{id}", request );

    public Task<bool> DeleteLessonAsync( Guid id )
        => DeleteAsync( $"api/admin/lessons/{id}" );

    public async Task<string?> UploadPdfAsync( Guid id, IBrowserFile file )
    {
        const long maxFileSize = 10 * 1024 * 1024;
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxFileSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent, "file", file.Name );

        var result = await PostFormAsync<UploadPdfResponse>( $"api/admin/lessons/{id}/upload/pdf", content );
        return result?.PdfPath;
    }

    public async Task<string?> UploadVideoAsync( Guid id, IBrowserFile file )
    {
        const long maxFileSize = 500L * 1024 * 1024; // 500 MB — matches server limit
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxFileSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent, "file", file.Name );

        var result = await PostFormAsync<UploadVideoResponse>( $"api/admin/lessons/{id}/upload/video", content );
        return result?.VideoPath;
    }

    public Task<bool> PublishLessonAsync( Guid id )
        => ActionAsync( $"api/admin/lessons/{id}/publish" );

    private record UploadPdfResponse( string PdfPath );
    private record UploadVideoResponse( string VideoPath );
}
