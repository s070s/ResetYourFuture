using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Infrastructure.Services;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Endpoint for serving lesson assets (PDF, video) with authorization.
/// Students must be enrolled in the course to access lesson assets.
///
/// [AllowAnonymous] because the browser-rendered &lt;video&gt;/&lt;iframe&gt; requests that hit
/// this endpoint cannot attach an Authorization header (SEC-2): a signed-in caller (cookie or
/// Bearer header — ASP.NET Core still populates User even under [AllowAnonymous]) is trusted as
/// usual, and everyone else must present a short-lived, single-lesson-scoped assetToken minted
/// by IAuthService.CreateLessonAssetTokenAsync instead of the general access JWT this used to
/// accept via ?access_token=.
/// </summary>
[ApiController]
[Route("api/lessons")]
[AllowAnonymous]
[Tags("Lesson Assets")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class LessonAssetsController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<LessonAssetsController> _logger;

    public LessonAssetsController(
        IApplicationDbContext db,
        IFileStorage fileStorage,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<LessonAssetsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get a lesson asset (PDF or video) if user is enrolled in the course.
    /// </summary>
    /// <param name="lessonId">Lesson ID</param>
    /// <param name="type">Asset type: "pdf" or "video"</param>
    /// <param name="assetToken">
    /// Required only when the caller has no Authorization header/cookie (the browser
    /// &lt;video&gt;/&lt;iframe&gt; case) — a token from <c>CreateLessonAssetTokenAsync</c>.
    /// </param>
    [HttpGet("{lessonId:guid}/asset")]
    public async Task<IActionResult> GetAsset(Guid lessonId, [FromQuery] string type, [FromQuery] string? assetToken)
    {
        string userId;
        if (User.Identity?.IsAuthenticated == true)
        {
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("User ID not found");
        }
        else if (!string.IsNullOrEmpty(assetToken) && TryResolveAssetToken(assetToken, lessonId, out var tokenUserId))
        {
            userId = tokenUserId;
        }
        else
        {
            return Problem(detail: "Authentication required.", statusCode: StatusCodes.Status401Unauthorized);
        }

        // Get lesson with course information
        var lesson = await _db.Lessons
            .Include(l => l.Module)
            .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson == null)
        {
            return Problem(detail: "Lesson not found", statusCode: StatusCodes.Status404NotFound);
        }

        // Check if user is enrolled in the course
        var isEnrolled = await _db.Enrollments
            .AnyAsync(e => e.UserId == userId && e.CourseId == lesson.Module.Course.Id);

        if (!isEnrolled)
        {
            // NOTE: Forbid(string) treats its argument as an auth scheme name (not a message),
            // which throws and yields a 500. Use an explicit 403 like the other controllers.
            return Problem(detail: "You must be enrolled in this course to access lesson assets", statusCode: StatusCodes.Status403Forbidden);
        }

        // Get file path based on type
        string? filePath = type.ToLowerInvariant() switch
        {
            "pdf" => lesson.PdfPath,
            "video" => lesson.VideoPath,
            _ => null
        };

        if (string.IsNullOrEmpty(filePath))
        {
            return Problem(detail: $"No {type} asset found for this lesson", statusCode: StatusCodes.Status404NotFound);
        }

        // Check if file exists
        if (!_fileStorage.FileExists(filePath))
        {
            _logger.LogWarning("File not found in storage: {FilePath}", filePath);
            return Problem(detail: "Asset file not found", statusCode: StatusCodes.Status404NotFound);
        }

        // Stream file
        try
        {
            var (stream, contentType) = await _fileStorage.GetFileAsync(filePath);
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming file: {FilePath}", filePath);
            return Problem(detail: "Error retrieving asset", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private bool TryResolveAssetToken(string assetToken, Guid lessonId, out string userId)
    {
        userId = string.Empty;
        try
        {
            var protector = _dataProtectionProvider
                .CreateProtector(AuthService.LessonAssetProtectorPurpose)
                .ToTimeLimitedDataProtector();
            var payload = protector.Unprotect(assetToken);
            var ticket = JsonSerializer.Deserialize<LessonAssetTicket>(payload);
            if (ticket is null || ticket.LessonId != lessonId)
                return false;

            userId = ticket.UserId;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lesson asset token invalid or expired for lesson {LessonId}.", lessonId);
            return false;
        }
    }
}
