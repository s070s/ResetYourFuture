using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Enums;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Student-facing and public certificate endpoints.
/// </summary>
[ApiController]
[Route( "api/[controller]" )]
[Authorize]
public class CertificatesController : ControllerBase
{
    // EF Core DB context used to query certificate and enrollment data.
    private readonly ApplicationDbContext _db;
    // Service that handles certificate issuance, revocation, and PDF generation.
    private readonly ICertificateService _certificateService;
    // File storage abstraction used to stream PDF files to the client.
    private readonly IFileStorage _storage;
    // Logger used to record operational events and errors for this controller.
    private readonly ILogger<CertificatesController> _logger;

    public CertificatesController(
        ApplicationDbContext db ,
        ICertificateService certificateService ,
        IFileStorage storage ,
        ILogger<CertificatesController> logger )
    {
        _db = db;
        _certificateService = certificateService;
        _storage = storage;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue( ClaimTypes.NameIdentifier )
        ?? throw new UnauthorizedAccessException( "User ID not found in claims" );

    /// <summary>
    /// Returns all active certificates for the authenticated student.
    /// </summary>
    [HttpGet( "my" )]
    public async Task<ActionResult<List<CertificateDto>>> GetMyCertificates(
        [FromQuery] string lang = "en" )
    {
        var userId = UserId;
        var isEl = string.Equals( lang , "el" , StringComparison.OrdinalIgnoreCase );

        var certificates = await _db.Certificates
            .AsNoTracking()
            .Where( c => c.UserId == userId && c.Status == CertificateStatus.Active )
            .OrderByDescending( c => c.IssuedAt )
            .Select( c => new CertificateDto(
                c.Id ,
                c.VerificationId ,
                c.RecipientName ,
                isEl ? ( c.CourseTitleEl ?? c.CourseTitleEn ) : c.CourseTitleEn ,
                c.IssuedAt ,
                c.Status.ToString()
            ) )
            .ToListAsync();

        return Ok( certificates );
    }

    /// <summary>
    /// Issues a certificate for a completed course. Idempotent: safe to call multiple times.
    /// </summary>
    [HttpPost( "issue/{courseId:guid}" )]
    public async Task<ActionResult<CertificateDto>> IssueCertificate(
        Guid courseId ,
        [FromQuery] string lang = "en" )
    {
        var userId = UserId;
        var isEl = string.Equals( lang , "el" , StringComparison.OrdinalIgnoreCase );

        try
        {
            var certificate = await _certificateService.GetOrGenerateAsync( userId , courseId );
            var courseTitle = isEl
                ? ( certificate.CourseTitleEl ?? certificate.CourseTitleEn )
                : certificate.CourseTitleEn;

            return Ok( new CertificateDto(
                certificate.Id ,
                certificate.VerificationId ,
                certificate.RecipientName ,
                courseTitle ,
                certificate.IssuedAt ,
                certificate.Status.ToString()
            ) );
        }
        catch ( InvalidOperationException ex )
        {
            _logger.LogWarning( ex , "Certificate issuance failed for user {UserId} on course {CourseId}." ,
                userId , courseId );
            return BadRequest( ex.Message );
        }
    }

    /// <summary>
    /// Streams the PDF for a certificate owned by the authenticated student.
    /// </summary>
    [HttpGet( "{certificateId:guid}/download" )]
    public async Task<IActionResult> DownloadCertificate( Guid certificateId )
    {
        var userId = UserId;

        var certificate = await _db.Certificates
            .FirstOrDefaultAsync( c => c.Id == certificateId && c.UserId == userId );

        if ( certificate is null )
            return NotFound( "Certificate not found." );

        if ( certificate.Status == CertificateStatus.Revoked )
            return BadRequest( "This certificate has been revoked." );

        if ( string.IsNullOrEmpty( certificate.PdfPath ) || !_storage.FileExists( certificate.PdfPath ) )
            return NotFound( "Certificate PDF is not available." );

        var ( stream , contentType ) = await _storage.GetFileAsync( certificate.PdfPath );
        var fileName = ToSafeFileName( $"Certificate - {certificate.RecipientName} - {certificate.CourseTitleEn}" ) + ".pdf";
        return File( stream , contentType , fileName );
    }

    private static string ToSafeFileName( string input )
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var safe = string.Concat( input.Select( c => invalid.Contains( c ) ? '_' : c ) );
        return safe.Length > 100 ? safe[..100].TrimEnd() : safe;
    }

    /// <summary>
    /// Public endpoint — verifies a certificate by its public VerificationId.
    /// No authentication required; intended for third-party verification.
    /// </summary>
    [HttpGet( "verify/{verificationId:guid}" )]
    [AllowAnonymous]
    public async Task<ActionResult<CertificateVerificationDto>> Verify(
        Guid verificationId ,
        [FromQuery] string lang = "en" )
    {
        var isEl = string.Equals( lang , "el" , StringComparison.OrdinalIgnoreCase );

        var certificate = await _db.Certificates
            .AsNoTracking()
            .FirstOrDefaultAsync( c => c.VerificationId == verificationId );

        if ( certificate is null )
        {
            return Ok( new CertificateVerificationDto(
                false , null , null , null , null , "Certificate not found." ) );
        }

        var isRevoked = certificate.Status == CertificateStatus.Revoked;
        var courseTitle = isEl
            ? ( certificate.CourseTitleEl ?? certificate.CourseTitleEn )
            : certificate.CourseTitleEn;

        return Ok( new CertificateVerificationDto(
            !isRevoked ,
            isRevoked ? null : certificate.RecipientName ,
            isRevoked ? null : courseTitle ,
            isRevoked ? null : certificate.IssuedAt ,
            certificate.Status.ToString() ,
            isRevoked ? "This certificate has been revoked." : "Certificate is valid."
        ) );
    }
}
