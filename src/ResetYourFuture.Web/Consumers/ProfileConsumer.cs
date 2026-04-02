using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the profile API.
/// </summary>
public class ProfileConsumer( HttpClient http ) : ApiClientBase( http ), IProfileConsumer
{
    public Task<ProfileDto?> GetProfileAsync()
        => GetAsync<ProfileDto>( "api/profile" );

    public Task<ProfileDto?> UpdateProfileAsync( UpdateProfileRequest request )
        => PutJsonAsync<UpdateProfileRequest, ProfileDto>( "api/profile", request );

    public async Task<(byte[] Data, string ContentType)?> GetAvatarAsync()
    {
        var response = await Http.GetAsync( "api/profile/avatar" );
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
        content.Add( streamContent, "file", file.Name );
        return await PostFormActionAsync( "api/profile/avatar", content );
    }

    public Task<bool> ChangePasswordAsync( ChangePasswordRequest request )
        => PostJsonActionAsync( "api/profile/change-password", request );
}
