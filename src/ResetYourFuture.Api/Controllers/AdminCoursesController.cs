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
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AdminCoursesController> _logger;

    public AdminCoursesController(ApplicationDbContext db, ILogger<AdminCoursesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get all courses (published and unpublished).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AdminCourseDto>>> GetCourses()
    {
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

        return Ok(courses);
    }

    /// <summary>
    /// Create a new course.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AdminCourseDto>> CreateCourse([FromBody] SaveCourseRequest request)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            IsPublished = false,
            UpdatedByUserId = UserId
        };

        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var dto = new AdminCourseDto(
            course.Id,
            course.Title,
            course.Description,
            course.IsPublished,
            course.CreatedAt,
            course.UpdatedAt,
            0, 0, 0
        );

        return CreatedAtAction(nameof(GetCourses), new { id = course.Id }, dto);
    }

    /// <summary>
    /// Update an existing course.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminCourseDto>> UpdateCourse(Guid id, [FromBody] SaveCourseRequest request)
    {
        var course = await _db.Courses
            .Include(c => c.Modules)
            .ThenInclude(m => m.Lessons)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
        {
            return NotFound();
        }

        course.Title = request.Title;
        course.Description = request.Description;
        course.UpdatedAt = DateTimeOffset.UtcNow;
        course.UpdatedByUserId = UserId;

        await _db.SaveChangesAsync();

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

        return Ok(dto);
    }

    /// <summary>
    /// Delete a course (only if no enrollments).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCourse(Guid id)
    {
        var course = await _db.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
        {
            return NotFound();
        }

        if (course.Enrollments.Any())
        {
            return BadRequest("Cannot delete course with existing enrollments");
        }

        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Publish a course (make it available to students).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishCourse(Guid id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        if (!course.IsPublished)
        {
            course.IsPublished = true;
            course.PublishedAt = DateTimeOffset.UtcNow;
            course.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
