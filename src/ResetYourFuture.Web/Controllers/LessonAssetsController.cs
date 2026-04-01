using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.ApiInterfaces;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Endpoint for serving lesson assets (PDF, video) with authorization.
/// Students must be enrolled in the course to access lesson assets.
/// </summary>
[ApiController]
[Route("api/lessons")]
[Authorize]
public class LessonAssetsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<LessonAssetsController> _logger;

    public LessonAssetsController(ApplicationDbContext db, IFileStorage fileStorage, ILogger<LessonAssetsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get a lesson asset (PDF or video) if user is enrolled in the course.
    /// </summary>
    /// <param name="lessonId">Lesson ID</param>
    /// <param name="type">Asset type: "pdf" or "video"</param>
    [HttpGet("{lessonId:guid}/asset")]
    public async Task<IActionResult> GetAsset(Guid lessonId, [FromQuery] string type)
    {
        // Get lesson with course information
        var lesson = await _db.Lessons
            .Include(l => l.Module)
            .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson == null)
        {
            return NotFound("Lesson not found");
        }

        // Check if user is enrolled in the course
        var isEnrolled = await _db.Enrollments
            .AnyAsync(e => e.UserId == UserId && e.CourseId == lesson.Module.Course.Id);

        if (!isEnrolled)
        {
            return Forbid("You must be enrolled in this course to access lesson assets");
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
            return NotFound($"No {type} asset found for this lesson");
        }

        // Check if file exists
        if (!_fileStorage.FileExists(filePath))
        {
            _logger.LogWarning("File not found in storage: {FilePath}", filePath);
            return NotFound("Asset file not found");
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
            return StatusCode(500, "Error retrieving asset");
        }
    }
}
