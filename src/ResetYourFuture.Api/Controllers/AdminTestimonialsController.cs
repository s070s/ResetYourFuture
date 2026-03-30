using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin-only CRUD endpoints for testimonial management.
/// </summary>
[ApiController]
[Route( "api/admin/testimonials" )]
[Authorize( Policy = "AdminOnly" )]
public class AdminTestimonialsController : ControllerBase
{
    private readonly ITestimonialService _testimonials;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<AdminTestimonialsController> _logger;

    public AdminTestimonialsController(
        ITestimonialService testimonials,
        IFileStorage fileStorage,
        ILogger<AdminTestimonialsController> logger )
    {
        _testimonials = testimonials;
        _fileStorage  = fileStorage;
        _logger       = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminTestimonialDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default )
    {
        var result = await _testimonials.GetAllForAdminAsync( page, pageSize, cancellationToken );
        return Ok( result );
    }

    [HttpGet( "{id:guid}" )]
    public async Task<ActionResult<AdminTestimonialDto>> GetById(
        Guid id, CancellationToken cancellationToken = default )
    {
        var item = await _testimonials.GetByIdAsync( id, cancellationToken );
        return item is null ? NotFound() : Ok( item );
    }

    [HttpPost]
    public async Task<ActionResult<AdminTestimonialDto>> Create(
        [FromBody] SaveTestimonialRequest request,
        CancellationToken cancellationToken = default )
    {
        var result = await _testimonials.CreateAsync( request, cancellationToken );
        return CreatedAtAction( nameof( GetById ), new { id = result.Id }, result );
    }

    [HttpPut( "{id:guid}" )]
    public async Task<ActionResult<AdminTestimonialDto>> Update(
        Guid id,
        [FromBody] SaveTestimonialRequest request,
        CancellationToken cancellationToken = default )
    {
        var result = await _testimonials.UpdateAsync( id, request, cancellationToken );
        return result is null ? NotFound() : Ok( result );
    }

    [HttpPost( "{id:guid}/toggle-active" )]
    public async Task<ActionResult<AdminTestimonialDto>> ToggleActive(
        Guid id, CancellationToken cancellationToken = default )
    {
        var result = await _testimonials.ToggleActiveAsync( id, cancellationToken );
        return result is null ? NotFound() : Ok( result );
    }

    [HttpPost( "{id:guid}/move-up" )]
    public async Task<IActionResult> MoveUp(
        Guid id, CancellationToken cancellationToken = default )
    {
        var success = await _testimonials.MoveUpAsync( id, cancellationToken );
        return success ? NoContent() : NotFound();
    }

    [HttpPost( "{id:guid}/move-down" )]
    public async Task<IActionResult> MoveDown(
        Guid id, CancellationToken cancellationToken = default )
    {
        var success = await _testimonials.MoveDownAsync( id, cancellationToken );
        return success ? NoContent() : NotFound();
    }

    [HttpPost( "{id:guid}/upload/avatar" )]
    public async Task<IActionResult> UploadAvatar(
        Guid id, IFormFile file, CancellationToken cancellationToken = default )
    {
        if ( file is null || file.Length == 0 )
            return BadRequest( "No file provided." );

        if ( file.Length > 5 * 1024 * 1024 )
            return BadRequest( "File exceeds the 5 MB limit." );

        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if ( !allowedTypes.Contains( file.ContentType, StringComparer.OrdinalIgnoreCase ) )
            return BadRequest( "Only image files are allowed (jpeg, png, gif, webp)." );

        var existing = await _testimonials.GetByIdAsync( id, cancellationToken );
        if ( existing is null )
            return NotFound();

        // Delete old avatar if it was an uploaded file
        if ( !string.IsNullOrEmpty( existing.AvatarPath ) )
            await _fileStorage.DeleteFileAsync( existing.AvatarPath );

        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync( stream, file.FileName, "testimonials/avatars", cancellationToken );

        await _testimonials.SetAvatarPathAsync( id, path, cancellationToken );

        _logger.LogInformation( "Avatar uploaded for testimonial {Id}: {Path}", id, path );
        return Ok( new { avatarPath = path } );
    }

    [HttpDelete( "{id:guid}/avatar" )]
    public async Task<IActionResult> RemoveAvatar(
        Guid id, CancellationToken cancellationToken = default )
    {
        var existing = await _testimonials.GetByIdAsync( id, cancellationToken );
        if ( existing is null )
            return NotFound();

        if ( !string.IsNullOrEmpty( existing.AvatarPath ) )
            await _fileStorage.DeleteFileAsync( existing.AvatarPath );

        var success = await _testimonials.RemoveAvatarAsync( id, cancellationToken );
        return success ? NoContent() : NotFound();
    }

    [HttpDelete( "{id:guid}" )]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken = default )
    {
        // Delete associated avatar file if present
        var existing = await _testimonials.GetByIdAsync( id, cancellationToken );
        if ( existing is not null && !string.IsNullOrEmpty( existing.AvatarPath ) )
            await _fileStorage.DeleteFileAsync( existing.AvatarPath );

        var success = await _testimonials.DeleteAsync( id, cancellationToken );
        return success ? NoContent() : NotFound();
    }
}
