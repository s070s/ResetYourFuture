using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin endpoints for managing courses.
/// </summary>
[ApiController]
[Route("api/admin/courses")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Courses")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminCoursesController(IAdminCourseService adminCourseService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get a single course by id (published or unpublished) with modules, lessons and enrollments.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminCourseDto>> GetCourseById(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await adminCourseService.GetCourseByIdAsync(id, cancellationToken);
        return dto is not null ? Ok(dto) : NotFound();
    }

    /// <summary>
    /// Get all courses (published and unpublished) with server-side pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCourseDto>>> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await adminCourseService.GetCoursesAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new course.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<AdminCourseDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminCourseDto>> CreateCourse([FromBody] SaveCourseRequest request, CancellationToken cancellationToken = default)
    {
        var dto = await adminCourseService.CreateCourseAsync(request, UserId, cancellationToken);
        return CreatedAtAction(nameof(GetCourseById), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Update an existing course.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminCourseDto>> UpdateCourse(Guid id, [FromBody] SaveCourseRequest request, CancellationToken cancellationToken = default)
    {
        var dto = await adminCourseService.UpdateCourseAsync(id, request, UserId, cancellationToken);
        return dto is not null ? Ok(dto) : NotFound();
    }

    /// <summary>
    /// Delete a course and its related data.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCourse(Guid id, CancellationToken cancellationToken = default)
    {
        return await adminCourseService.DeleteCourseAsync(id, UserId, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Publish a course (make it available to students).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishCourse(Guid id, CancellationToken cancellationToken = default)
    {
        return await adminCourseService.PublishCourseAsync(id, UserId, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Unpublish a course (hide it from students).
    /// </summary>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> UnpublishCourse(Guid id, CancellationToken cancellationToken = default)
    {
        return await adminCourseService.UnpublishCourseAsync(id, UserId, cancellationToken) ? NoContent() : NotFound();
    }
}
