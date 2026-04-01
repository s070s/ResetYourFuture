using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin endpoints for managing courses.
/// </summary>
[ApiController]
[Route( "api/admin/courses" )]
[Authorize( Policy = "AdminOnly" )]
public class AdminCoursesController : ControllerBase
{
    // EF Core DB context used to query and persist course-related data.
    private readonly ApplicationDbContext _db;
    // Logger used to record operational events and errors for this controller.
    private readonly ILogger<AdminCoursesController> _logger;

    // Constructor receives dependencies (DB context and logger) via DI.
    public AdminCoursesController( ApplicationDbContext db , ILogger<AdminCoursesController> logger )
    {
        _db = db;
        _logger = logger;
    }

    // Helper property to get the current authenticated user's ID or throw if missing.
    private string UserId => User.FindFirstValue( ClaimTypes.NameIdentifier )
        ?? throw new UnauthorizedAccessException( "User ID not found" );

    /// <summary>
    /// Get a single course by id (published or unpublished) with modules, lessons and enrollments.
    /// </summary>
    [HttpGet( "{id:guid}" )]
    public async Task<ActionResult<AdminCourseDto>> GetCourseById( Guid id )
    {
        var course = await _db.Courses
            .AsNoTracking()
            .Include( c => c.Modules )
            .ThenInclude( m => m.Lessons )
            .Include( c => c.Enrollments )
            .FirstOrDefaultAsync( c => c.Id == id );

        if ( course == null )
        {
            return NotFound();
        }

        var dto = new AdminCourseDto(
            course.Id ,
            course.TitleEn ,
            course.TitleEl ,
            course.DescriptionEn ,
            course.DescriptionEl ,
            course.IsPublished ,
            course.CreatedAt ,
            course.UpdatedAt ,
            course.Modules.Count ,
            course.Modules.SelectMany( m => m.Lessons ).Count() ,
            course.Enrollments.Count ,
            course.RequiredTier
        );

        return Ok( dto );
    }

    /// <summary>
    /// Get all courses (published and unpublished) with server-side pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCourseDto>>> GetCourses(
        [FromQuery] int page = 1 ,
        [FromQuery] int pageSize = 10 ,
        CancellationToken cancellationToken = default )
    {
        page = Math.Max( 1 , page );
        pageSize = Math.Clamp( pageSize , 1 , 100 );

        var totalCount = await _db.Courses.CountAsync( cancellationToken );

        var items = await _db.Courses
            .AsNoTracking()
            .Include( c => c.Modules )
            .ThenInclude( m => m.Lessons )
            .Include( c => c.Enrollments )
            .OrderByDescending( c => c.CreatedAt )
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .Select( c => new AdminCourseDto(
                c.Id ,
                c.TitleEn ,
                c.TitleEl ,
                c.DescriptionEn ,
                c.DescriptionEl ,
                c.IsPublished ,
                c.CreatedAt ,
                c.UpdatedAt ,
                c.Modules.Count ,
                c.Modules.SelectMany( m => m.Lessons ).Count() ,
                c.Enrollments.Count ,
                c.RequiredTier
            ) )
            .ToListAsync( cancellationToken );

        return Ok( new PagedResult<AdminCourseDto>( items , totalCount , page , pageSize ) );
    }

    /// <summary>
    /// Create a new course.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AdminCourseDto>> CreateCourse( [FromBody] SaveCourseRequest request )
    {
        // Build a new Course entity with provided values and initial metadata.
        var course = new Course
        {
            Id = Guid.NewGuid() ,
            TitleEn = request.TitleEn ,
            TitleEl = request.TitleEl ,
            DescriptionEn = request.DescriptionEn ,
            DescriptionEl = request.DescriptionEl ,
            RequiredTier = request.RequiredTier ,
            IsPublished = false ,
            UpdatedByUserId = UserId
        };

        // Add the new course to the DbContext and persist changes.
        _db.Courses.Add( course );
        await _db.SaveChangesAsync();

        // Map the persisted entity to the AdminCourseDto for the response.
        var dto = new AdminCourseDto(
            course.Id ,
            course.TitleEn ,
            course.TitleEl ,
            course.DescriptionEn ,
            course.DescriptionEl ,
            course.IsPublished ,
            course.CreatedAt ,
            course.UpdatedAt ,
            0 , 0 , 0 ,
            course.RequiredTier
        );

        // Return 201 Created with a location header pointing to the courses endpoint.
        return CreatedAtAction( nameof( GetCourses ) , new
        {
            id = course.Id
        } , dto );
    }

    /// <summary>
    /// Update an existing course.
    /// </summary>
    [HttpPut( "{id:guid}" )]
    public async Task<ActionResult<AdminCourseDto>> UpdateCourse( Guid id , [FromBody] SaveCourseRequest request )
    {
        // Load the course with related modules/lessons and enrollments for DTO values.
        var course = await _db.Courses
            .Include( c => c.Modules )
            .ThenInclude( m => m.Lessons )
            .Include( c => c.Enrollments )
            .FirstOrDefaultAsync( c => c.Id == id );

        // Return 404 if the course does not exist.
        if ( course == null )
        {
            return NotFound();
        }

        // Apply updates to the entity and set audit metadata.
        course.TitleEn = request.TitleEn;
        course.TitleEl = request.TitleEl;
        course.DescriptionEn = request.DescriptionEn;
        course.DescriptionEl = request.DescriptionEl;
        course.RequiredTier = request.RequiredTier;
        course.UpdatedAt = DateTimeOffset.UtcNow;
        course.UpdatedByUserId = UserId;

        // Persist the updated entity to the database.
        await _db.SaveChangesAsync();

        // Map the updated entity to DTO and return it to the caller.
        var dto = new AdminCourseDto(
            course.Id ,
            course.TitleEn ,
            course.TitleEl ,
            course.DescriptionEn ,
            course.DescriptionEl ,
            course.IsPublished ,
            course.CreatedAt ,
            course.UpdatedAt ,
            course.Modules.Count ,
            course.Modules.SelectMany( m => m.Lessons ).Count() ,
            course.Enrollments.Count ,
            course.RequiredTier
        );

        // Return 200 OK with the updated course DTO.
        return Ok( dto );
    }

    /// <summary>
    /// Delete a course (only if no enrollments).
    /// </summary>
    [HttpDelete( "{id:guid}" )]
    public async Task<IActionResult> DeleteCourse( Guid id )
    {
        // Load the course including all related data for cascade deletion.
        var course = await _db.Courses
            .Include( c => c.Enrollments )
            .Include( c => c.Modules )
                .ThenInclude( m => m.Lessons )
            .FirstOrDefaultAsync( c => c.Id == id );

        // Return 404 when the course cannot be found.
        if ( course == null )
        {
            return NotFound();
        }

        // Remove lesson completions for all lessons in this course.
        var lessonIds = course.Modules.SelectMany( m => m.Lessons ).Select( l => l.Id ).ToList();
        if ( lessonIds.Count > 0 )
        {
            var completions = await _db.LessonCompletions
                .Where( lc => lessonIds.Contains( lc.LessonId ) )
                .ToListAsync();
            _db.LessonCompletions.RemoveRange( completions );
        }

        // Remove enrollments for this course.
        if ( course.Enrollments.Any() )
        {
            _db.Enrollments.RemoveRange( course.Enrollments );
        }

        // Remove the course entity (cascade will remove modules and lessons).
        _db.Courses.Remove( course );
        await _db.SaveChangesAsync();

        _logger.LogInformation( "Admin {UserId} deleted course {CourseId} with {Enrollments} enrollment(s)" ,
            UserId , id , course.Enrollments.Count );

        // Return 204 No Content to indicate successful deletion.
        return NoContent();
    }

    /// <summary>
    /// Publish a course (make it available to students).
    /// </summary>
    [HttpPost( "{id:guid}/publish" )]
    public async Task<IActionResult> PublishCourse( Guid id )
    {
        // Find the course by ID to update its published state.
        var course = await _db.Courses.FindAsync( id );
        if ( course == null )
        {
            return NotFound();
        }

        // If not already published, mark published, set timestamps and user, then save.
        if ( !course.IsPublished )
        {
            course.IsPublished = true;
            course.PublishedAt = DateTimeOffset.UtcNow;
            course.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        // Return 204 No Content to indicate success.
        return NoContent();
    }

    /// <summary>
    /// Unpublish a course (hide it from students).
    /// </summary>
    [HttpPost( "{id:guid}/unpublish" )]
    public async Task<IActionResult> UnpublishCourse( Guid id )
    {
        var course = await _db.Courses.FindAsync( id );
        if ( course == null )
        {
            return NotFound();
        }

        if ( course.IsPublished )
        {
            course.IsPublished = false;
            course.UpdatedAt = DateTimeOffset.UtcNow;
            course.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
