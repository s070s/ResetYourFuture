using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for profile-related API operations.
/// </summary>
public interface IProfileConsumer
{
    Task<ProfileDto?> GetProfileAsync();
    Task<ProfileDto?> UpdateProfileAsync( UpdateProfileRequest request );
    Task<(byte[] Data, string ContentType)?> GetAvatarAsync();
    Task<bool> UploadAvatarAsync( IBrowserFile file );
    Task<bool> ChangePasswordAsync( ChangePasswordRequest request );
}
