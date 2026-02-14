using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.Models.Profile;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// User profile management endpoints.
/// </summary>
[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(UserManager<ApplicationUser> userManager, IFileStorage fileStorage, ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get current user's profile.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ProfileDto>> GetProfile()
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null)
        {
            return NotFound();
        }

        var dto = new ProfileDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            user.DisplayName,
            user.AvatarPath,
            user.DateOfBirth
        );

        return Ok(dto);
    }

    /// <summary>
    /// Update current user's profile.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ProfileDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null)
        {
            return NotFound();
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.DisplayName = request.DisplayName?.Trim();
        user.DateOfBirth = request.DateOfBirth;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        var dto = new ProfileDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            user.DisplayName,
            user.AvatarPath,
            user.DateOfBirth
        );

        return Ok(dto);
    }

    /// <summary>
    /// Upload user avatar.
    /// </summary>
    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        const long maxAvatarSize = 5 * 1024 * 1024; // 5 MB
        if (file.Length > maxAvatarSize)
        {
            return BadRequest("File too large (max 5 MB)");
        }

        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null)
        {
            return NotFound();
        }

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarPath))
        {
            await _fileStorage.DeleteFileAsync(user.AvatarPath);
        }

        // Save new avatar
        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync(stream, file.FileName, "avatars");

        user.AvatarPath = path;
        await _userManager.UpdateAsync(user);

        return Ok(new { avatarPath = path });
    }

    /// <summary>
    /// Get avatar image for current or specified user.
    /// </summary>
    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar()
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null || string.IsNullOrEmpty(user.AvatarPath))
        {
            return NotFound();
        }

        if (!_fileStorage.FileExists(user.AvatarPath))
        {
            return NotFound();
        }

        var (stream, contentType) = await _fileStorage.GetFileAsync(user.AvatarPath);
        return File(stream, contentType);
    }

    /// <summary>
    /// Change password.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null)
        {
            return NotFound();
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        return NoContent();
    }
}
