using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Interfaces;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Site-wide settings management.
/// </summary>
[ApiController]
[Route("api/site")]
public class SiteSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<SiteSettingsController> _logger;

    public SiteSettingsController(ApplicationDbContext db, IFileStorage fileStorage, ILogger<SiteSettingsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get landing page background image (public endpoint).
    /// </summary>
    [HttpGet("background-image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBackgroundImage()
    {
        var setting = await _db.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "LandingBackgroundImage");

        if (setting == null || string.IsNullOrEmpty(setting.Value))
        {
            return NotFound();
        }

        if (!_fileStorage.FileExists(setting.Value))
        {
            return NotFound();
        }

        var (stream, contentType) = await _fileStorage.GetFileAsync(setting.Value);
        return File(stream, contentType);
    }

    /// <summary>
    /// Upload landing page background image (admin only).
    /// </summary>
    [HttpPost("admin/background-image")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UploadBackgroundImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        var setting = await _db.SiteSettings
            .FirstOrDefaultAsync(s => s.Key == "LandingBackgroundImage");

        // Delete old background if exists
        if (setting != null && !string.IsNullOrEmpty(setting.Value))
        {
            await _fileStorage.DeleteFileAsync(setting.Value);
        }

        // Save new background
        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync(stream, file.FileName, "backgrounds");

        if (setting == null)
        {
            setting = new SiteSetting
            {
                Id = Guid.NewGuid(),
                Key = "LandingBackgroundImage",
                Value = path,
                UpdatedByUserId = UserId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.SiteSettings.Add(setting);
        }
        else
        {
            setting.Value = path;
            setting.UpdatedByUserId = UserId;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new { backgroundImagePath = path });
    }
}
