using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Application.ApiServices;

public class ProfileService(
    UserManager<ApplicationUser> userManager,
    IFileStorage fileStorage,
    ILogger<ProfileService> logger) : IProfileService
{
    public async Task<ProfileDto?> GetProfileAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : ToDto(user);
    }

    public async Task<ServiceResult<ProfileDto>> UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult<ProfileDto>.NotFound();

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.DisplayName = request.DisplayName?.Trim();
        user.DateOfBirth = request.DateOfBirth;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return ServiceResult<ProfileDto>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        return ServiceResult<ProfileDto>.Ok(ToDto(user));
    }

    public async Task<ServiceResult<AvatarUploadResultDto>> UploadAvatarAsync(string userId, Stream fileStream, string fileName)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult<AvatarUploadResultDto>.NotFound();

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarPath))
        {
            await fileStorage.DeleteFileAsync(user.AvatarPath);
        }

        // Save new avatar
        var path = await fileStorage.SaveFileAsync(fileStream, fileName, "avatars");

        user.AvatarPath = path;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            logger.LogError("Failed to persist avatar path for user {UserId}: {Errors}",
                userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            return new ServiceResult<AvatarUploadResultDto>(null, StatusCodes.Status500InternalServerError, "Failed to save avatar.");
        }

        return ServiceResult<AvatarUploadResultDto>.Ok(new AvatarUploadResultDto(path));
    }

    public async Task<(Stream Stream, string ContentType)?> GetAvatarAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.AvatarPath))
            return null;

        if (!fileStorage.FileExists(user.AvatarPath))
            return null;

        return await fileStorage.GetFileAsync(user.AvatarPath);
    }

    public async Task<ServiceResult<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult<bool>.NotFound();

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return ServiceResult<bool>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        return ServiceResult<bool>.NoContent();
    }

    private static ProfileDto ToDto(ApplicationUser user) => new(
        user.Id,
        user.Email ?? "",
        user.FirstName,
        user.LastName,
        user.DisplayName,
        user.AvatarPath,
        user.DateOfBirth
    );
}
