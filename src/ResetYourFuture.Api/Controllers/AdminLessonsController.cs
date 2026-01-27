using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.Models.Admin;
using System.Security.Claims;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin endpoints for managing lessons.
/// </summary>
[ApiController]
[Route("api/admin/lessons")]
[Authorize(Policy = "AdminOnly")]
public class AdminLessonsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;

    public AdminLessonsController(ApplicationDbContext db, IFileStorage fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("module/{moduleId:guid}")]
    public async Task<ActionResult<List<AdminLessonDto>>> GetLessonsByModule(Guid moduleId)
    {
        var lessons = await _db.Lessons
            .Where(l => l.ModuleId == moduleId)
            .OrderBy(l => l.SortOrder)
            .Select(l => new AdminLessonDto(
                l.Id,
                l.Title,
                l.Content,
                l.PdfPath,
                l.VideoPath,
                l.DurationMinutes,
                l.SortOrder,
                l.ModuleId,
                l.IsPublished
            ))
            .ToListAsync();

        return Ok(lessons);
    }

    [HttpPost]
    public async Task<ActionResult<AdminLessonDto>> CreateLesson([FromBody] SaveLessonRequest request)
    {
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            DurationMinutes = request.DurationMinutes,
            SortOrder = request.SortOrder,
            ModuleId = request.ModuleId,
            IsPublished = false,
            UpdatedByUserId = UserId
        };

        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync();

        var dto = new AdminLessonDto(
            lesson.Id,
            lesson.Title,
            lesson.Content,
            lesson.PdfPath,
            lesson.VideoPath,
            lesson.DurationMinutes,
            lesson.SortOrder,
            lesson.ModuleId,
            lesson.IsPublished
        );

        return CreatedAtAction(nameof(GetLessonsByModule), new { moduleId = lesson.ModuleId }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminLessonDto>> UpdateLesson(Guid id, [FromBody] SaveLessonRequest request)
    {
        var lesson = await _db.Lessons.FindAsync(id);
        if (lesson == null) return NotFound();

        lesson.Title = request.Title;
        lesson.Content = request.Content;
        lesson.DurationMinutes = request.DurationMinutes;
        lesson.SortOrder = request.SortOrder;
        lesson.UpdatedAt = DateTimeOffset.UtcNow;
        lesson.UpdatedByUserId = UserId;

        await _db.SaveChangesAsync();

        var dto = new AdminLessonDto(
            lesson.Id,
            lesson.Title,
            lesson.Content,
            lesson.PdfPath,
            lesson.VideoPath,
            lesson.DurationMinutes,
            lesson.SortOrder,
            lesson.ModuleId,
            lesson.IsPublished
        );

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLesson(Guid id)
    {
        var lesson = await _db.Lessons.FindAsync(id);
        if (lesson == null) return NotFound();

        // Delete associated files
        if (!string.IsNullOrEmpty(lesson.PdfPath))
        {
            await _fileStorage.DeleteFileAsync(lesson.PdfPath);
        }
        if (!string.IsNullOrEmpty(lesson.VideoPath))
        {
            await _fileStorage.DeleteFileAsync(lesson.VideoPath);
        }

        _db.Lessons.Remove(lesson);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/upload/pdf")]
    public async Task<IActionResult> UploadPdf(Guid id, IFormFile file)
    {
        var lesson = await _db.Lessons.FindAsync(id);
        if (lesson == null) return NotFound();

        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        // Delete old PDF if exists
        if (!string.IsNullOrEmpty(lesson.PdfPath))
        {
            await _fileStorage.DeleteFileAsync(lesson.PdfPath);
        }

        // Save new PDF
        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync(stream, file.FileName, "lessons/pdf");

        lesson.PdfPath = path;
        lesson.UpdatedAt = DateTimeOffset.UtcNow;
        lesson.UpdatedByUserId = UserId;
        await _db.SaveChangesAsync();

        return Ok(new { pdfPath = path });
    }

    [HttpPost("{id:guid}/upload/video")]
    public async Task<IActionResult> UploadVideo(Guid id, IFormFile file)
    {
        var lesson = await _db.Lessons.FindAsync(id);
        if (lesson == null) return NotFound();

        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        // Delete old video if exists
        if (!string.IsNullOrEmpty(lesson.VideoPath))
        {
            await _fileStorage.DeleteFileAsync(lesson.VideoPath);
        }

        // Save new video
        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync(stream, file.FileName, "lessons/video");

        lesson.VideoPath = path;
        lesson.UpdatedAt = DateTimeOffset.UtcNow;
        lesson.UpdatedByUserId = UserId;
        await _db.SaveChangesAsync();

        return Ok(new { videoPath = path });
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishLesson(Guid id)
    {
        var lesson = await _db.Lessons.FindAsync(id);
        if (lesson == null) return NotFound();

        if (!lesson.IsPublished)
        {
            lesson.IsPublished = true;
            lesson.PublishedAt = DateTimeOffset.UtcNow;
            lesson.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
