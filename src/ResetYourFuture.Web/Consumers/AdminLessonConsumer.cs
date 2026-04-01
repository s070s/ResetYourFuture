using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin lesson management API.
/// </summary>
public class AdminLessonConsumer : IAdminLessonConsumer
{
    private readonly HttpClient _http;

    public AdminLessonConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<List<AdminLessonDto>> GetLessonsByModuleAsync( Guid moduleId )
    {
        var response = await _http.GetAsync( $"api/admin/lessons/module/{moduleId}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<AdminLessonDto>>() ?? []
            : [];
    }

    public async Task<AdminLessonDto?> CreateLessonAsync( SaveLessonRequest request )
    {
        var response = await _http.PostAsJsonAsync( "api/admin/lessons" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminLessonDto>()
            : null;
    }

    public async Task<AdminLessonDto?> UpdateLessonAsync( Guid id , SaveLessonRequest request )
    {
        var response = await _http.PutAsJsonAsync( $"api/admin/lessons/{id}" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminLessonDto>()
            : null;
    }

    public async Task<bool> DeleteLessonAsync( Guid id )
    {
        var response = await _http.DeleteAsync( $"api/admin/lessons/{id}" );
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> UploadPdfAsync( Guid id , IBrowserFile file )
    {
        const long maxFileSize = 10 * 1024 * 1024; // 10 MB
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxFileSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent , "file" , file.Name );

        var response = await _http.PostAsync( $"api/admin/lessons/{id}/upload/pdf" , content );
        if ( !response.IsSuccessStatusCode )
            return null;

        var result = await response.Content.ReadFromJsonAsync<UploadPdfResponse>();
        return result?.PdfPath;
    }

    public async Task<string?> UploadVideoAsync( Guid id , IBrowserFile file )
    {
        const long maxFileSize = 500L * 1024 * 1024; // 500 MB — matches server limit
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxFileSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent , "file" , file.Name );

        var response = await _http.PostAsync( $"api/admin/lessons/{id}/upload/video" , content );
        if ( !response.IsSuccessStatusCode )
            return null;

        var result = await response.Content.ReadFromJsonAsync<UploadVideoResponse>();
        return result?.VideoPath;
    }

    public async Task<bool> PublishLessonAsync( Guid id )
    {
        var response = await _http.PostAsync( $"api/admin/lessons/{id}/publish" , null );
        return response.IsSuccessStatusCode;
    }

    private record UploadPdfResponse( string PdfPath );
    private record UploadVideoResponse( string VideoPath );
}
