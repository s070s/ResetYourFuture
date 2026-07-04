using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Current user's profile: get/update, avatar upload/download, and password change.
/// </summary>
public interface IProfileService
{
    Task<ProfileDto?> GetProfileAsync(string userId);
    Task<ServiceResult<ProfileDto>> UpdateProfileAsync(string userId, UpdateProfileRequest request);

    /// <summary>Deletes any existing avatar, saves the new file, and updates the user record.</summary>
    Task<ServiceResult<AvatarUploadResultDto>> UploadAvatarAsync(string userId, Stream fileStream, string fileName);
    Task<(Stream Stream, string ContentType)?> GetAvatarAsync(string userId);
    Task<ServiceResult<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request);
}
