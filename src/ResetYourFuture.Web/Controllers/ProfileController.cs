using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Extensions;

using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// User profile management endpoints.
/// </summary>
[ApiController]
[Route("api/profile")]
[Authorize]
[Tags("Profile")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get current user's profile.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ProfileDto>> GetProfile()
    {
        var dto = await profileService.GetProfileAsync(UserId);
        return dto is not null ? Ok(dto) : NotFound();
    }

    /// <summary>
    /// Update current user's profile.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ProfileDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var result = await profileService.UpdateProfileAsync(UserId, request);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Upload user avatar.
    /// </summary>
    [HttpPost("avatar")]
    public async Task<ActionResult<AvatarUploadResultDto>> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ErrorMessagesRes.NoFileProvided);

        const long maxAvatarSize = 5 * 1024 * 1024; // 5 MB
        if (file.Length > maxAvatarSize)
            return BadRequest(ErrorMessagesRes.FileTooLarge5Mb);

        var allowedAvatarTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if (!allowedAvatarTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(ErrorMessagesRes.OnlyImageFilesAllowed);

        using var stream = file.OpenReadStream();
        var result = await profileService.UploadAvatarAsync(UserId, stream, file.FileName);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Get avatar image for current or specified user.
    /// </summary>
    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar()
    {
        var avatar = await profileService.GetAvatarAsync(UserId);
        if (avatar is null)
            return NotFound();

        var (stream, contentType) = avatar.Value;
        return File(stream, contentType);
    }

    /// <summary>
    /// Change password.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<ActionResult<bool>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await profileService.ChangePasswordAsync(UserId, request);
        return result.ToActionResult(this);
    }
}
