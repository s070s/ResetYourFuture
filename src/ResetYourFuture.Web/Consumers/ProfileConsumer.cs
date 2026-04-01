using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the profile API.
/// </summary>
public class ProfileConsumer : IProfileConsumer
{
    private readonly HttpClient _http;

    public ProfileConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<ProfileDto?> GetProfileAsync()
    {
        var response = await _http.GetAsync( "api/profile" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProfileDto>()
            : null;
    }

    public async Task<ProfileDto?> UpdateProfileAsync( UpdateProfileRequest request )
    {
        var response = await _http.PutAsJsonAsync( "api/profile" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProfileDto>()
            : null;
    }

    public async Task<(byte[] Data, string ContentType)?> GetAvatarAsync()
    {
        var response = await _http.GetAsync( "api/profile/avatar" );
        if ( !response.IsSuccessStatusCode )
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
        return (bytes, contentType);
    }

    public async Task<bool> UploadAvatarAsync( IBrowserFile file )
    {
        const long maxFileSize = 5 * 1024 * 1024;
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxFileSize );
        var streamContent = new StreamContent( stream );
        streamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        content.Add( streamContent , "file" , file.Name );
        var response = await _http.PostAsync( "api/profile/avatar" , content );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ChangePasswordAsync( ChangePasswordRequest request )
    {
        var response = await _http.PostAsJsonAsync( "api/profile/change-password" , request );
        return response.IsSuccessStatusCode;
    }
}
