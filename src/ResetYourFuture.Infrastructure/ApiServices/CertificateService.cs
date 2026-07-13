using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Application.ApiInterfaces;

namespace ResetYourFuture.Infrastructure.ApiServices;

/// <summary>
/// Issues, revokes, and regenerates PDF certificates for completed course enrollments.
/// </summary>
public class CertificateService : ICertificateService
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly INotificationDispatcher _notifications;
    private readonly ILogger<CertificateService> _logger;

    static CertificateService()
    {
        // Set once when the class is first loaded. Community license is free for
        // non-commercial and open-source use.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public CertificateService(
        IApplicationDbContext db,
        IFileStorage storage,
        INotificationDispatcher notifications,
        ILogger<CertificateService> logger)
    {
        _db = db;
        _storage = storage;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Certificate> GetOrGenerateAsync(
        string userId,
        Guid courseId,
        CancellationToken cancellationToken = default)
    {
        // Idempotency: return the existing certificate without recreating anything
        var existing = await _db.Certificates
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CourseId == courseId, cancellationToken);

        if (existing is not null)
            return existing;

        // Validate that the enrollment exists and is fully completed
        var enrollment = await _db.Enrollments
            .Include(e => e.Course)
            .Include(e => e.User)
            .FirstOrDefaultAsync(
                e => e.UserId == userId
                  && e.CourseId == courseId
                  && e.Status == EnrollmentStatus.Completed,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"No completed enrollment found for user {userId} on course {courseId}.");

        var course = enrollment.Course;
        var user = enrollment.User;

        // Sum lesson durations; null DurationMinutes are treated as zero
        var totalDuration = await _db.Lessons
            .Where(l => l.Module.CourseId == courseId)
            .SumAsync(l => l.DurationMinutes ?? 0, cancellationToken);

        // Prefer DisplayName, fall back to first + last name
        var recipientName = !string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.DisplayName
            : $"{user.FirstName} {user.LastName}".Trim();

        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            VerificationId = Guid.NewGuid(),
            UserId = userId,
            CourseId = courseId,
            EnrollmentId = enrollment.Id,
            RecipientName = recipientName,
            CourseTitleEn = course.TitleEn,
            CourseTitleEl = course.TitleEl,
            TotalDurationMinutes = totalDuration > 0 ? totalDuration : null,
            IssuedAt = DateTime.UtcNow,
            Status = CertificateStatus.Active
        };

        certificate.PdfPath = await GeneratePdfAsync(certificate, cancellationToken);

        _db.Certificates.Add(certificate);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request won the race and inserted first (unique index on UserId+CourseId).
            // Clean up the PDF we just wrote and return the already-committed certificate.
            try { await _storage.DeleteFileAsync(certificate.PdfPath, cancellationToken); }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx,
                    "Could not delete orphaned PDF {Path} after duplicate-certificate race.",
                    certificate.PdfPath);
            }

            var winner = await _db.Certificates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.CourseId == courseId, cancellationToken);

            if (winner is not null)
                return winner;

            throw; // Unexpected: unique violation but no row found — re-throw the original exception.
        }

        _logger.LogInformation(
            "Certificate {CertificateId} issued for user {UserId} on course {CourseId}.",
            certificate.Id, userId, courseId);

        await _notifications.DispatchAsync(
            userId,
            NotificationType.CertificateIssued,
            "CertificateIssued",
            [course.TitleEn],
            "/my-certificates",
            cancellationToken);

        return certificate;
    }

    public async Task RevokeAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        var certificate = await _db.Certificates.FindAsync([certificateId], cancellationToken)
            ?? throw new KeyNotFoundException($"Certificate {certificateId} not found.");

        certificate.Status = CertificateStatus.Revoked;
        certificate.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Certificate {CertificateId} revoked.", certificateId);
    }

    public async Task RegenerateAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        var certificate = await _db.Certificates.FindAsync([certificateId], cancellationToken)
            ?? throw new KeyNotFoundException($"Certificate {certificateId} not found.");

        if (!string.IsNullOrEmpty(certificate.PdfPath) && _storage.FileExists(certificate.PdfPath))
            await _storage.DeleteFileAsync(certificate.PdfPath, cancellationToken);

        certificate.PdfPath = await GeneratePdfAsync(certificate, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Certificate {CertificateId} PDF regenerated.", certificateId);
    }

    // ---------------------------------------------------------------------------
    // PDF generation
    // ---------------------------------------------------------------------------

    private async Task<string> GeneratePdfAsync(Certificate cert, CancellationToken cancellationToken)
    {
        // Render the certificate in the culture active for the current request
        // (set by UseRequestLocalization). Defaults to English outside a localized request.
        var pdfBytes = BuildDocument(cert, CultureInfo.CurrentUICulture);

        using var stream = new MemoryStream(pdfBytes);
        return await _storage.SaveFileAsync(
            stream,
            $"{cert.VerificationId}.pdf",
            "certificates",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Selects the course title to print for the given culture.
    /// Greek cultures (el) get the Greek title when one exists; every other culture
    /// gets the English title. Fixes the prior behaviour that always preferred Greek.
    /// </summary>
    public static string ResolveCourseTitle(string titleEn, string? titleEl, CultureInfo culture)
    {
        var isGreek = culture.TwoLetterISOLanguageName.Equals("el", StringComparison.OrdinalIgnoreCase);
        return isGreek && !string.IsNullOrWhiteSpace(titleEl) ? titleEl : titleEn;
    }

    private static byte[] BuildDocument(Certificate cert, CultureInfo culture)
    {
        var courseTitle = ResolveCourseTitle(cert.CourseTitleEn, cert.CourseTitleEl, culture);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(60);
                page.MarginVertical(50);

                page.Content().Column(col =>
                {
                    col.Spacing(14);

                    // Platform name
                    col.Item().AlignCenter()
                        .Text("Reset Your Future")
                        .FontSize(11)
                        .FontColor("#888888")
                        .LetterSpacing(0.1f);

                    // Divider
                    col.Item().PaddingVertical(2)
                        .LineHorizontal(1)
                        .LineColor("#dddddd");

                    // Main heading
                    col.Item().AlignCenter()
                        .Text("Certificate of Completion")
                        .FontSize(32)
                        .Bold()
                        .FontColor("#1e3a5f");

                    col.Item().AlignCenter()
                        .Text("This is to certify that")
                        .FontSize(13)
                        .FontColor("#777777");

                    // Recipient
                    col.Item().AlignCenter()
                        .Text(cert.RecipientName)
                        .FontSize(26)
                        .Bold()
                        .FontColor("#111111");

                    col.Item().AlignCenter()
                        .Text("has successfully completed")
                        .FontSize(13)
                        .FontColor("#777777");

                    // Course title — localised for the issuing culture (English by default)
                    col.Item().AlignCenter()
                        .Text(courseTitle)
                        .FontSize(20)
                        .Bold()
                        .FontColor("#1e3a5f");

                    // Optional duration
                    if (cert.TotalDurationMinutes is > 0)
                    {
                        var hours = cert.TotalDurationMinutes.Value / 60;
                        var minutes = cert.TotalDurationMinutes.Value % 60;
                        var label = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";

                        col.Item().AlignCenter()
                            .Text($"Total duration: {label}")
                            .FontSize(11)
                            .FontColor("#888888");
                    }

                    // Divider
                    col.Item().PaddingVertical(2)
                        .LineHorizontal(1)
                        .LineColor("#dddddd");

                    // Issue date
                    col.Item().AlignCenter()
                        .Text($"Issued on {cert.IssuedAt:MMMM dd, yyyy}")
                        .FontSize(12)
                        .FontColor("#555555");

                    // Verification ID
                    col.Item().AlignCenter()
                        .Text($"Verification ID: {cert.VerificationId}")
                        .FontSize(9)
                        .FontColor("#aaaaaa");
                });
            });
        }).GeneratePdf();
    }
}
