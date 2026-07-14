using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Application.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin endpoints for managing modules within courses.
/// </summary>
[ApiController]
[Route("api/admin/modules")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Modules")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminModulesController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly IHtmlSanitizer _sanitizer;

    public AdminModulesController(IApplicationDbContext db, IHtmlSanitizer sanitizer)
    {
        _db = db;
        _sanitizer = sanitizer;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Get all modules for a course (with lesson counts), ordered by sort order.</summary>
    [HttpGet("course/{courseId:guid}")]
    public async Task<ActionResult<List<AdminModuleDto>>> GetModulesByCourse(Guid courseId)
    {
        var modules = await _db.Modules
            .AsNoTracking()
            .Where(m => m.CourseId == courseId)
            .Include(m => m.Lessons)
            .OrderBy(m => m.SortOrder)
            .Select(CourseContentMappings.ModuleAdminProjection)
            .ToListAsync();

        return Ok(modules);
    }

    /// <summary>Get a single module (with lesson count) by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminModuleDto>> GetModuleById(Guid id)
    {
        var module = await _db.Modules
            .AsNoTracking()
            .Include(m => m.Lessons)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (module == null)
            return NotFound();

        return Ok(module.ToAdminDto());
    }

    /// <summary>Create a new module within a course.</summary>
    [HttpPost]
    [ProducesResponseType<AdminModuleDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminModuleDto>> CreateModule([FromBody] SaveModuleRequest request)
    {
        var module = new Module
        {
            Id = Guid.NewGuid(),
            TitleEn = request.TitleEn,
            TitleEl = request.TitleEl,
            DescriptionEn = request.DescriptionEn is not null ? _sanitizer.Sanitize(request.DescriptionEn) : null,
            DescriptionEl = request.DescriptionEl is not null ? _sanitizer.Sanitize(request.DescriptionEl) : null,
            SortOrder = request.SortOrder,
            CourseId = request.CourseId,
            UpdatedByUserId = UserId
        };

        _db.Modules.Add(module);
        await _db.SaveChangesAsync();

        // API-4: point Location at the by-id GET for the module just created, not the
        // course-scoped list of all its siblings.
        return CreatedAtAction(nameof(GetModuleById), new
        {
            id = module.Id
        }, module.ToAdminDto());
    }

    /// <summary>Update an existing module by id.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminModuleDto>> UpdateModule(Guid id, [FromBody] SaveModuleRequest request)
    {
        var module = await _db.Modules
            .Include(m => m.Lessons)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (module == null)
            return NotFound();

        module.TitleEn = request.TitleEn;
        module.TitleEl = request.TitleEl;
        module.DescriptionEn = request.DescriptionEn is not null ? _sanitizer.Sanitize(request.DescriptionEn) : null;
        module.DescriptionEl = request.DescriptionEl is not null ? _sanitizer.Sanitize(request.DescriptionEl) : null;
        module.SortOrder = request.SortOrder;
        module.UpdatedAt = DateTimeOffset.UtcNow;
        module.UpdatedByUserId = UserId;

        await _db.SaveChangesAsync();

        return Ok(module.ToAdminDto());
    }

    /// <summary>Delete a module and cascade-delete its lessons and their completion records.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteModule(Guid id)
    {
        var module = await _db.Modules
            .Include(m => m.Lessons)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (module == null)
            return NotFound();

        // Remove lesson completions for all lessons in this module before deleting.
        if (module.Lessons.Any())
        {
            var lessonIds = module.Lessons.Select(l => l.Id).ToList();
            var completions = await _db.LessonCompletions
                .Where(lc => lessonIds.Contains(lc.LessonId))
                .ToListAsync();
            _db.LessonCompletions.RemoveRange(completions);
        }

        // Remove the module (cascade will remove its lessons).
        _db.Modules.Remove(module);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
