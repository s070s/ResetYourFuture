using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.Mappings;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Student-facing and public certificate endpoints.
/// </summary>
[ApiController]
[Route("api/certificates")]
[Authorize]
[Tags("Certificates")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class CertificatesController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICertificateService _certificateService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IFileStorage _storage;
    private readonly ILogger<CertificatesController> _logger;

    public CertificatesController(
        IApplicationDbContext db,
        ICertificateService certificateService,
        ISubscriptionService subscriptionService,
        IFileStorage storage,
        ILogger<CertificatesController> logger)
    {
        _db = db;
        _certificateService = certificateService;
        _subscriptionService = subscriptionService;
        _storage = storage;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found in claims");

    /// <summary>
    /// Returns all active certificates for the authenticated student.
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<List<CertificateDto>>> GetMyCertificates(
        [FromQuery] string lang = "en")
    {
        var userId = UserId;
        var isEl = Localized.IsEl(lang);

        var certificates = await _db.Certificates
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.Status == CertificateStatus.Active)
            .OrderByDescending(c => c.IssuedAt)
            .Select(CertificateMappings.Projection(isEl))
            .ToListAsync();

        return Ok(certificates);
    }

    /// <summary>
    /// Issues a certificate for a completed course. Idempotent: safe to call multiple times.
    /// </summary>
    [HttpPost("issue/{courseId:guid}")]
    public async Task<ActionResult<CertificateDto>> IssueCertificate(
        Guid courseId,
        [FromQuery] string lang = "en")
    {
        var userId = UserId;
        var isEl = Localized.IsEl(lang);

        var subStatus = await _subscriptionService.GetUserStatusAsync(userId);
        if (subStatus.Features?.CertificateAccess != true)
            return Problem(
                detail: "Your current plan does not include certificate access. Upgrade to Pro.",
                statusCode: StatusCodes.Status403Forbidden);

        try
        {
            var certificate = await _certificateService.GetOrGenerateAsync(userId, courseId);
            return Ok(certificate.ToDto(isEl));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Certificate issuance failed for user {UserId} on course {CourseId}.",
                userId, courseId);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Streams the PDF for a certificate owned by the authenticated student.
    /// </summary>
    [HttpGet("{certificateId:guid}/download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> DownloadCertificate(Guid certificateId)
    {
        var userId = UserId;

        var certificate = await _db.Certificates
            .FirstOrDefaultAsync(c => c.Id == certificateId && c.UserId == userId);

        if (certificate is null)
            return Problem(detail: "Certificate not found.", statusCode: StatusCodes.Status404NotFound);

        if (certificate.Status == CertificateStatus.Revoked)
            return Problem(detail: "This certificate has been revoked.", statusCode: StatusCodes.Status400BadRequest);

        if (string.IsNullOrEmpty(certificate.PdfPath) || !_storage.FileExists(certificate.PdfPath))
            return Problem(detail: "Certificate PDF is not available.", statusCode: StatusCodes.Status404NotFound);

        var (stream, contentType) = await _storage.GetFileAsync(certificate.PdfPath);
        var fileName = ToSafeFileName($"Certificate - {certificate.RecipientName} - {certificate.CourseTitleEn}") + ".pdf";
        return File(stream, contentType, fileName);
    }

    private static string ToSafeFileName(string input)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var safe = string.Concat(input.Select(c => invalid.Contains(c) ? '_' : c));
        return safe.Length > 100 ? safe[..100].TrimEnd() : safe;
    }

    /// <summary>
    /// Public endpoint — verifies a certificate by its public VerificationId.
    /// No authentication required; intended for third-party verification.
    /// </summary>
    [HttpGet("verify/{verificationId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<CertificateVerificationDto>> Verify(
        Guid verificationId,
        [FromQuery] string lang = "en")
    {
        var isEl = Localized.IsEl(lang);

        var certificate = await _db.Certificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.VerificationId == verificationId);

        if (certificate is null)
        {
            return Ok(new CertificateVerificationDto(
                false, null, null, null, null, "Certificate not found."));
        }

        return Ok(certificate.ToVerificationDto(isEl));
    }
}
