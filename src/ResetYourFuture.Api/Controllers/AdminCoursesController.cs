using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Shared.Models.Admin;
using System.Security.Claims;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin endpoints for managing courses.
/// </summary>
[ApiController]
[Route("api/admin/courses")]
[Authorize(Policy = "AdminOnly")]
public class AdminCoursesController : ControllerBase
{
    // EF Core DB context used to query and persist course-related data.
    private readonly ApplicationDbContext _db;
    // Logger used to record operational events and errors for this controller.
    private readonly ILogger<AdminCoursesController> _logger;

    // Constructor receives dependencies (DB context and logger) via DI.
    public AdminCoursesController(ApplicationDbContext db, ILogger<AdminCoursesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Helper property to get the current authenticated user's ID or throw if missing.
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get all courses (published and unpublished).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AdminCourseDto>>> GetCourses()
    {
        // Query courses including related modules, lessons and enrollments and project to DTOs.
        var courses = await _db.Courses
            .Include(c => c.Modules)
            .ThenInclude(m => m.Lessons)
            .Include(c => c.Enrollments)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new AdminCourseDto(
                c.Id,
                c.Title,
                c.Description,
                c.IsPublished,
                c.CreatedAt,
                c.UpdatedAt,
                c.Modules.Count,
                c.Modules.SelectMany(m => m.Lessons).Count(),
                c.Enrollments.Count
            ))
            .ToListAsync();

        // Return 200 OK with the list of courses for the admin UI.
        return Ok(courses);
    }

    /// <summary>
    /// Create a new course.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AdminCourseDto>> CreateCourse([FromBody] SaveCourseRequest request)
    {
        // Build a new Course entity with provided values and initial metadata.
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            IsPublished = false,
            UpdatedByUserId = UserId
        };

        // Add the new course to the DbContext and persist changes.
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        // Map the persisted entity to the AdminCourseDto for the response.
        var dto = new AdminCourseDto(
            course.Id,
            course.Title,
            course.Description,
            course.IsPublished,
            course.CreatedAt,
            course.UpdatedAt,
            0, 0, 0
        );

        // Return 201 Created with a location header pointing to the courses endpoint.
        return CreatedAtAction(nameof(GetCourses), new { id = course.Id }, dto);
    }

    /// <summary>
    /// Update an existing course.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminCourseDto>> UpdateCourse(Guid id, [FromBody] SaveCourseRequest request)
    {
        // Load the course with related modules/lessons and enrollments for DTO values.
        var course = await _db.Courses
            .Include(c => c.Modules)
            .ThenInclude(m => m.Lessons)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id);

        // Return 404 if the course does not exist.
        if (course == null)
        {
            return NotFound();
        }

        // Apply updates to the entity and set audit metadata.
        course.Title = request.Title;
        course.Description = request.Description;
        course.UpdatedAt = DateTimeOffset.UtcNow;
        course.UpdatedByUserId = UserId;

        // Persist the updated entity to the database.
        await _db.SaveChangesAsync();

        // Map the updated entity to DTO and return it to the caller.
        var dto = new AdminCourseDto(
            course.Id,
            course.Title,
            course.Description,
            course.IsPublished,
            course.CreatedAt,
            course.UpdatedAt,
            course.Modules.Count,
            course.Modules.SelectMany(m => m.Lessons).Count(),
            course.Enrollments.Count
        );

        // Return 200 OK with the updated course DTO.
        return Ok(dto);
    }

    /// <summary>
    /// Delete a course (only if no enrollments).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCourse(Guid id)
    {
        // Load the course including enrollments so we can check for existing enrollments.
        var course = await _db.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id);

        // Return 404 when the course cannot be found.
        if (course == null)
        {
            return NotFound();
        }

        // Prevent deletion if there are existing enrollments associated with this course.
        if (course.Enrollments.Any())
        {
            return BadRequest("Cannot delete course with existing enrollments");
        }

        // Remove the course entity and persist the deletion.
        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();

        // Return 204 No Content to indicate successful deletion.
        return NoContent();
    }

    /// <summary>
    /// Publish a course (make it available to students).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishCourse(Guid id)
    {
        // Find the course by ID to update its published state.
        var course = await _db.Courses.FindAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        // If not already published, mark published, set timestamps and user, then save.
        if (!course.IsPublished)
        {
            course.IsPublished = true;
            course.PublishedAt = DateTimeOffset.UtcNow;
            course.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        // Return 204 No Content to indicate success.
        return NoContent();
    }
}
