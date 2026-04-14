using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Domain.Enums;
using ResetYourFuture.Web.ApiInterfaces;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Issues, revokes, and regenerates PDF certificates for completed course enrollments.
/// </summary>
public class CertificateService : ICertificateService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ILogger<CertificateService> _logger;

    static CertificateService()
    {
        // Set once when the class is first loaded. Community license is free for
        // non-commercial and open-source use.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public CertificateService(
        ApplicationDbContext db ,
        IFileStorage storage ,
        ILogger<CertificateService> logger )
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<Certificate> GetOrGenerateAsync(
        string userId ,
        Guid courseId ,
        CancellationToken cancellationToken = default )
    {
        // Idempotency: return the existing certificate without recreating anything
        var existing = await _db.Certificates
            .FirstOrDefaultAsync( c => c.UserId == userId && c.CourseId == courseId , cancellationToken );

        if ( existing is not null )
            return existing;

        // Validate that the enrollment exists and is fully completed
        var enrollment = await _db.Enrollments
            .Include( e => e.Course )
            .Include( e => e.User )
            .FirstOrDefaultAsync(
                e => e.UserId == userId
                  && e.CourseId == courseId
                  && e.Status == EnrollmentStatus.Completed ,
                cancellationToken )
            ?? throw new InvalidOperationException(
                $"No completed enrollment found for user {userId} on course {courseId}." );

        var course = enrollment.Course;
        var user = enrollment.User;

        // Sum lesson durations; null DurationMinutes are treated as zero
        var totalDuration = await _db.Lessons
            .Where( l => l.Module.CourseId == courseId )
            .SumAsync( l => l.DurationMinutes ?? 0 , cancellationToken );

        // Prefer DisplayName, fall back to first + last name
        var recipientName = !string.IsNullOrWhiteSpace( user.DisplayName )
            ? user.DisplayName
            : $"{user.FirstName} {user.LastName}".Trim();

        var certificate = new Certificate
        {
            Id = Guid.NewGuid() ,
            VerificationId = Guid.NewGuid() ,
            UserId = userId ,
            CourseId = courseId ,
            EnrollmentId = enrollment.Id ,
            RecipientName = recipientName ,
            CourseTitleEn = course.TitleEn ,
            CourseTitleEl = course.TitleEl ,
            TotalDurationMinutes = totalDuration > 0 ? totalDuration : null ,
            IssuedAt = DateTime.UtcNow ,
            Status = CertificateStatus.Active
        };

        certificate.PdfPath = await GeneratePdfAsync( certificate , cancellationToken );

        _db.Certificates.Add( certificate );
        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation(
            "Certificate {CertificateId} issued for user {UserId} on course {CourseId}." ,
            certificate.Id , userId , courseId );

        return certificate;
    }

    public async Task RevokeAsync( Guid certificateId , CancellationToken cancellationToken = default )
    {
        var certificate = await _db.Certificates.FindAsync( [ certificateId ] , cancellationToken )
            ?? throw new KeyNotFoundException( $"Certificate {certificateId} not found." );

        certificate.Status = CertificateStatus.Revoked;
        certificate.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation( "Certificate {CertificateId} revoked." , certificateId );
    }

    public async Task RegenerateAsync( Guid certificateId , CancellationToken cancellationToken = default )
    {
        var certificate = await _db.Certificates.FindAsync( [ certificateId ] , cancellationToken )
            ?? throw new KeyNotFoundException( $"Certificate {certificateId} not found." );

        if ( !string.IsNullOrEmpty( certificate.PdfPath ) && _storage.FileExists( certificate.PdfPath ) )
            await _storage.DeleteFileAsync( certificate.PdfPath , cancellationToken );

        certificate.PdfPath = await GeneratePdfAsync( certificate , cancellationToken );
        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation( "Certificate {CertificateId} PDF regenerated." , certificateId );
    }

    // ---------------------------------------------------------------------------
    // PDF generation
    // ---------------------------------------------------------------------------

    private async Task<string> GeneratePdfAsync( Certificate cert , CancellationToken cancellationToken )
    {
        var pdfBytes = BuildDocument( cert );

        using var stream = new MemoryStream( pdfBytes );
        return await _storage.SaveFileAsync(
            stream ,
            $"{cert.VerificationId}.pdf" ,
            "certificates" ,
            cancellationToken );
    }

    private static byte[] BuildDocument( Certificate cert )
    {
        return Document.Create( container =>
        {
            container.Page( page =>
            {
                page.Size( PageSizes.A4.Landscape() );
                page.MarginHorizontal( 60 );
                page.MarginVertical( 50 );

                page.Content().Column( col =>
                {
                    col.Spacing( 14 );

                    // Platform name
                    col.Item().AlignCenter()
                        .Text( "Reset Your Future" )
                        .FontSize( 11 )
                        .FontColor( "#888888" )
                        .LetterSpacing( 0.1f );

                    // Divider
                    col.Item().PaddingVertical( 2 )
                        .LineHorizontal( 1 )
                        .LineColor( "#dddddd" );

                    // Main heading
                    col.Item().AlignCenter()
                        .Text( "Certificate of Completion" )
                        .FontSize( 32 )
                        .Bold()
                        .FontColor( "#1e3a5f" );

                    col.Item().AlignCenter()
                        .Text( "This is to certify that" )
                        .FontSize( 13 )
                        .FontColor( "#777777" );

                    // Recipient
                    col.Item().AlignCenter()
                        .Text( cert.RecipientName )
                        .FontSize( 26 )
                        .Bold()
                        .FontColor( "#111111" );

                    col.Item().AlignCenter()
                        .Text( "has successfully completed" )
                        .FontSize( 13 )
                        .FontColor( "#777777" );

                    // Course title
                    col.Item().AlignCenter()
                        .Text( cert.CourseTitleEn )
                        .FontSize( 20 )
                        .Bold()
                        .FontColor( "#1e3a5f" );

                    // Optional duration
                    if ( cert.TotalDurationMinutes is > 0 )
                    {
                        var hours = cert.TotalDurationMinutes.Value / 60;
                        var minutes = cert.TotalDurationMinutes.Value % 60;
                        var label = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";

                        col.Item().AlignCenter()
                            .Text( $"Total duration: {label}" )
                            .FontSize( 11 )
                            .FontColor( "#888888" );
                    }

                    // Divider
                    col.Item().PaddingVertical( 2 )
                        .LineHorizontal( 1 )
                        .LineColor( "#dddddd" );

                    // Issue date
                    col.Item().AlignCenter()
                        .Text( $"Issued on {cert.IssuedAt:MMMM dd, yyyy}" )
                        .FontSize( 12 )
                        .FontColor( "#555555" );

                    // Verification ID
                    col.Item().AlignCenter()
                        .Text( $"Verification ID: {cert.VerificationId}" )
                        .FontSize( 9 )
                        .FontColor( "#aaaaaa" );
                } );
            } );
        } ).GeneratePdf();
    }
}
