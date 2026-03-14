using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin endpoints for managing lessons.
/// </summary>
[ApiController]
[Route( "api/admin/lessons" )]
[Authorize( Policy = "AdminOnly" )]
public class AdminLessonsController : ControllerBase
{
    // EF Core DB context used to query and persist lessons and related data.
    private readonly ApplicationDbContext _db;
    // File storage abstraction used to save and delete lesson PDFs and videos.
    private readonly IFileStorage _fileStorage;

    // Constructor receives dependencies (DbContext and file storage) via DI.
    public AdminLessonsController( ApplicationDbContext db , IFileStorage fileStorage )
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    // Helper property to get the current authenticated user's ID (used for audit fields).
    private string UserId => User.FindFirstValue( ClaimTypes.NameIdentifier )!;

    // Get all lessons for a specific module, ordered by SortOrder.
    [HttpGet( "module/{moduleId:guid}" )]
    public async Task<ActionResult<List<AdminLessonDto>>> GetLessonsByModule( Guid moduleId )
    {
        // Query lessons for the module, order and project to DTOs to minimize payload.
        var lessons = await _db.Lessons
            .AsNoTracking()
            .Where( l => l.ModuleId == moduleId )
            .OrderBy( l => l.SortOrder )
            .Select( l => new AdminLessonDto(
                l.Id ,
                l.Title ,
                l.Content ,
                l.PdfPath ,
                l.VideoPath ,
                l.DurationMinutes ,
                l.SortOrder ,
                l.ModuleId ,
                l.IsPublished
            ) )
            .ToListAsync();

        // Return the list of lesson DTOs.
        return Ok( lessons );
    }

    // Create a new lesson under a module.
    [HttpPost]
    public async Task<ActionResult<AdminLessonDto>> CreateLesson( [FromBody] SaveLessonRequest request )
    {
        // Create a new Lesson entity with provided values and audit metadata.
        var lesson = new Lesson
        {
            Id = Guid.NewGuid() ,
            Title = request.Title ,
            Content = request.Content ,
            VideoPath = request.VideoUrl ,
            DurationMinutes = request.DurationMinutes ,
            SortOrder = request.SortOrder ,
            ModuleId = request.ModuleId ,
            IsPublished = false ,
            UpdatedByUserId = UserId
        };

        // Add the entity to the context and persist it.
        _db.Lessons.Add( lesson );
        await _db.SaveChangesAsync();

        // Map persisted entity to DTO for the response.
        var dto = new AdminLessonDto(
            lesson.Id ,
            lesson.Title ,
            lesson.Content ,
            lesson.PdfPath ,
            lesson.VideoPath ,
            lesson.DurationMinutes ,
            lesson.SortOrder ,
            lesson.ModuleId ,
            lesson.IsPublished
        );

        // Return 201 Created with a location pointing to the module's lessons.
        return CreatedAtAction( nameof( GetLessonsByModule ) , new
        {
            moduleId = lesson.ModuleId
        } , dto );
    }

    // Update an existing lesson by id.
    [HttpPut( "{id:guid}" )]
    public async Task<ActionResult<AdminLessonDto>> UpdateLesson( Guid id , [FromBody] SaveLessonRequest request )
    {
        // Find the lesson or return 404 if it does not exist.
        var lesson = await _db.Lessons.FindAsync( id );
        if ( lesson == null )
            return NotFound();

        // Apply updates to the entity and set audit metadata.
        lesson.Title = request.Title;
        lesson.Content = request.Content;
        lesson.VideoPath = request.VideoUrl ?? lesson.VideoPath;
        lesson.DurationMinutes = request.DurationMinutes;
        lesson.SortOrder = request.SortOrder;
        lesson.UpdatedAt = DateTimeOffset.UtcNow;
        lesson.UpdatedByUserId = UserId;

        // Persist changes to the database.
        await _db.SaveChangesAsync();

        // Map updated entity to DTO and return it.
        var dto = new AdminLessonDto(
            lesson.Id ,
            lesson.Title ,
            lesson.Content ,
            lesson.PdfPath ,
            lesson.VideoPath ,
            lesson.DurationMinutes ,
            lesson.SortOrder ,
            lesson.ModuleId ,
            lesson.IsPublished
        );

        // Return 200 OK with the updated DTO.
        return Ok( dto );
    }

    // Delete a lesson, its completion records, and any associated files.
    [HttpDelete( "{id:guid}" )]
    public async Task<IActionResult> DeleteLesson( Guid id )
    {
        // Find the lesson or return 404.
        var lesson = await _db.Lessons.FindAsync( id );
        if ( lesson == null )
            return NotFound();

        // Remove lesson completion records first (FK constraint).
        var completions = await _db.LessonCompletions
            .Where( lc => lc.LessonId == id )
            .ToListAsync();
        _db.LessonCompletions.RemoveRange( completions );

        // Delete associated PDF file if present.
        if ( !string.IsNullOrEmpty( lesson.PdfPath ) )
        {
            await _fileStorage.DeleteFileAsync( lesson.PdfPath );
        }
        // Delete associated video file if present (skip URLs — only delete uploaded files).
        if ( !string.IsNullOrEmpty( lesson.VideoPath ) && !lesson.VideoPath.StartsWith( "http" , StringComparison.OrdinalIgnoreCase ) )
        {
            await _fileStorage.DeleteFileAsync( lesson.VideoPath );
        }

        // Remove the lesson entity and persist deletion.
        _db.Lessons.Remove( lesson );
        await _db.SaveChangesAsync();

        // Return 204 No Content to indicate successful deletion.
        return NoContent();
    }

    // Upload a PDF for a lesson.
    [HttpPost( "{id:guid}/upload/pdf" )]
    public async Task<IActionResult> UploadPdf( Guid id , IFormFile file )
    {
        // Find the lesson or return 404.
        var lesson = await _db.Lessons.FindAsync( id );
        if ( lesson == null )
            return NotFound();

        // Validate that a file was provided.
        if ( file == null || file.Length == 0 )
        {
            return BadRequest( "No file provided" );
        }

        // Delete previous PDF if it exists to avoid orphaned files.
        if ( !string.IsNullOrEmpty( lesson.PdfPath ) )
        {
            await _fileStorage.DeleteFileAsync( lesson.PdfPath );
        }

        // Save the new PDF using the file storage abstraction.
        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync( stream , file.FileName , "lessons/pdf" );

        // Update the lesson entity with the new path and audit metadata, then persist.
        lesson.PdfPath = path;
        lesson.UpdatedAt = DateTimeOffset.UtcNow;
        lesson.UpdatedByUserId = UserId;
        await _db.SaveChangesAsync();

        // Return the stored PDF path to the caller.
        return Ok( new
        {
            pdfPath = path
        } );
    }

    // Upload a video for a lesson.
    [HttpPost( "{id:guid}/upload/video" )]
    public async Task<IActionResult> UploadVideo( Guid id , IFormFile file )
    {
        // Find the lesson or return 404.
        var lesson = await _db.Lessons.FindAsync( id );
        if ( lesson == null )
            return NotFound();

        // Validate file input.
        if ( file == null || file.Length == 0 )
        {
            return BadRequest( "No file provided" );
        }

        // Delete previous video if present to prevent orphaned files.
        if ( !string.IsNullOrEmpty( lesson.VideoPath ) )
        {
            await _fileStorage.DeleteFileAsync( lesson.VideoPath );
        }

        // Save the new video and get its storage path.
        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync( stream , file.FileName , "lessons/video" );

        // Update entity with new video path and audit metadata, then persist.
        lesson.VideoPath = path;
        lesson.UpdatedAt = DateTimeOffset.UtcNow;
        lesson.UpdatedByUserId = UserId;
        await _db.SaveChangesAsync();

        // Return the new video path.
        return Ok( new
        {
            videoPath = path
        } );
    }

    // Publish a lesson to make it available to students.
    [HttpPost( "{id:guid}/publish" )]
    public async Task<IActionResult> PublishLesson( Guid id )
    {
        // Find the lesson or return 404.
        var lesson = await _db.Lessons.FindAsync( id );
        if ( lesson == null )
            return NotFound();

        // If not already published, mark published and set audit metadata.
        if ( !lesson.IsPublished )
        {
            lesson.IsPublished = true;
            lesson.PublishedAt = DateTimeOffset.UtcNow;
            lesson.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        // Return 204 No Content for success without body.
        return NoContent();
    }
}
