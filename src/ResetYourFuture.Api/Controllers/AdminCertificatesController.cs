using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin endpoints for managing issued certificates.
/// </summary>
[ApiController]
[Route( "api/admin/certificates" )]
[Authorize( Policy = "AdminOnly" )]
public class AdminCertificatesController : ControllerBase
{
    // EF Core DB context used to query and list certificate records.
    private readonly ApplicationDbContext _db;
    // Service that handles certificate revocation and PDF regeneration.
    private readonly ICertificateService _certificateService;
    // Logger used to record operational events and errors for this controller.
    private readonly ILogger<AdminCertificatesController> _logger;

    public AdminCertificatesController(
        ApplicationDbContext db ,
        ICertificateService certificateService ,
        ILogger<AdminCertificatesController> logger )
    {
        _db = db;
        _certificateService = certificateService;
        _logger = logger;
    }

    /// <summary>
    /// Returns a paginated list of all issued certificates across all users.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCertificateListItemDto>>> GetAll(
        [FromQuery] int page = 1 ,
        [FromQuery] int pageSize = 20 )
    {
        if ( page < 1 ) page = 1;
        if ( pageSize < 1 || pageSize > 100 ) pageSize = 20;

        var query = _db.Certificates
            .AsNoTracking()
            .Include( c => c.User )
            .OrderByDescending( c => c.IssuedAt );

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .Select( c => new AdminCertificateListItemDto(
                c.Id ,
                c.VerificationId ,
                c.RecipientName ,
                c.User.Email ?? c.UserId ,
                c.CourseTitleEn ,
                c.IssuedAt ,
                c.Status.ToString()
            ) )
            .ToListAsync();

        return Ok( new PagedResult<AdminCertificateListItemDto>( items , totalCount , page , pageSize ) );
    }

    /// <summary>
    /// Revokes an active certificate.
    /// </summary>
    [HttpPost( "{certificateId:guid}/revoke" )]
    public async Task<IActionResult> Revoke( Guid certificateId )
    {
        try
        {
            await _certificateService.RevokeAsync( certificateId );
            return NoContent();
        }
        catch ( KeyNotFoundException ex )
        {
            return NotFound( ex.Message );
        }
    }

    /// <summary>
    /// Deletes and regenerates the PDF for an existing certificate.
    /// </summary>
    [HttpPost( "{certificateId:guid}/regenerate" )]
    public async Task<IActionResult> Regenerate( Guid certificateId )
    {
        try
        {
            await _certificateService.RegenerateAsync( certificateId );
            return NoContent();
        }
        catch ( KeyNotFoundException ex )
        {
            return NotFound( ex.Message );
        }
    }
}
