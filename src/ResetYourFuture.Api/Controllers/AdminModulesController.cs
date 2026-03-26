using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin endpoints for managing modules within courses.
/// </summary>
[ApiController]
[Route( "api/admin/modules" )]
[Authorize( Policy = "AdminOnly" )]
public class AdminModulesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminModulesController( ApplicationDbContext db )
    {
        _db = db;
    }

    private string UserId => User.FindFirstValue( ClaimTypes.NameIdentifier )!;

    [HttpGet( "course/{courseId:guid}" )]
    public async Task<ActionResult<List<AdminModuleDto>>> GetModulesByCourse( Guid courseId )
    {
        var modules = await _db.Modules
            .AsNoTracking()
            .Where( m => m.CourseId == courseId )
            .Include( m => m.Lessons )
            .OrderBy( m => m.SortOrder )
            .Select( m => new AdminModuleDto(
                m.Id ,
                m.TitleEn ,
                m.TitleEl ,
                m.DescriptionEn ,
                m.DescriptionEl ,
                m.SortOrder ,
                m.CourseId ,
                m.Lessons.Count
            ) )
            .ToListAsync();

        return Ok( modules );
    }

    [HttpGet( "{id:guid}" )]
    public async Task<ActionResult<AdminModuleDto>> GetModuleById( Guid id )
    {
        var module = await _db.Modules
            .AsNoTracking()
            .Include( m => m.Lessons )
            .FirstOrDefaultAsync( m => m.Id == id );

        if ( module == null )
            return NotFound();

        return Ok( new AdminModuleDto(
            module.Id ,
            module.TitleEn ,
            module.TitleEl ,
            module.DescriptionEn ,
            module.DescriptionEl ,
            module.SortOrder ,
            module.CourseId ,
            module.Lessons.Count
        ) );
    }

    [HttpPost]
    public async Task<ActionResult<AdminModuleDto>> CreateModule( [FromBody] SaveModuleRequest request )
    {
        var module = new Module
        {
            Id = Guid.NewGuid() ,
            TitleEn = request.TitleEn ,
            TitleEl = request.TitleEl ,
            DescriptionEn = request.DescriptionEn ,
            DescriptionEl = request.DescriptionEl ,
            SortOrder = request.SortOrder ,
            CourseId = request.CourseId ,
            UpdatedByUserId = UserId
        };

        _db.Modules.Add( module );
        await _db.SaveChangesAsync();

        var dto = new AdminModuleDto(
            module.Id ,
            module.TitleEn ,
            module.TitleEl ,
            module.DescriptionEn ,
            module.DescriptionEl ,
            module.SortOrder ,
            module.CourseId ,
            0
        );

        return CreatedAtAction( nameof( GetModulesByCourse ) , new
        {
            courseId = module.CourseId
        } , dto );
    }

    [HttpPut( "{id:guid}" )]
    public async Task<ActionResult<AdminModuleDto>> UpdateModule( Guid id , [FromBody] SaveModuleRequest request )
    {
        var module = await _db.Modules
            .Include( m => m.Lessons )
            .FirstOrDefaultAsync( m => m.Id == id );

        if ( module == null )
            return NotFound();

        module.TitleEn = request.TitleEn;
        module.TitleEl = request.TitleEl;
        module.DescriptionEn = request.DescriptionEn;
        module.DescriptionEl = request.DescriptionEl;
        module.SortOrder = request.SortOrder;
        module.UpdatedAt = DateTimeOffset.UtcNow;
        module.UpdatedByUserId = UserId;

        await _db.SaveChangesAsync();

        var dto = new AdminModuleDto(
            module.Id ,
            module.TitleEn ,
            module.TitleEl ,
            module.DescriptionEn ,
            module.DescriptionEl ,
            module.SortOrder ,
            module.CourseId ,
            module.Lessons.Count
        );

        return Ok( dto );
    }

    [HttpDelete( "{id:guid}" )]
    public async Task<IActionResult> DeleteModule( Guid id )
    {
        var module = await _db.Modules
            .Include( m => m.Lessons )
            .FirstOrDefaultAsync( m => m.Id == id );

        if ( module == null )
            return NotFound();

        // Remove lesson completions for all lessons in this module before deleting.
        if ( module.Lessons.Any() )
        {
            var lessonIds = module.Lessons.Select( l => l.Id ).ToList();
            var completions = await _db.LessonCompletions
                .Where( lc => lessonIds.Contains( lc.LessonId ) )
                .ToListAsync();
            _db.LessonCompletions.RemoveRange( completions );
        }

        // Remove the module (cascade will remove its lessons).
        _db.Modules.Remove( module );
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
