using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// Client consumer for student-facing and public certificate API operations.
/// </summary>
public interface ICertificateConsumer
{
    Task<List<CertificateDto>?> GetMyCertificatesAsync( string lang = "en" );
    Task<CertificateDto?> IssueCertificateAsync( Guid courseId, string lang = "en" );
    Task<byte[]?> DownloadCertificateAsync( Guid certificateId );
    Task<CertificateVerificationDto?> VerifyAsync( Guid verificationId, string lang = "en" );
}
