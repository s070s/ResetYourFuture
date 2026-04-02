using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for student-facing and public certificate API operations.
/// </summary>
public class CertificateConsumer( HttpClient http ) : ApiClientBase( http ), ICertificateConsumer
{
    public Task<List<CertificateDto>?> GetMyCertificatesAsync( string lang = "en" )
        => GetAsync<List<CertificateDto>>( $"api/certificates/my?lang={lang}" );

    public Task<CertificateDto?> IssueCertificateAsync( Guid courseId, string lang = "en" )
        => PostAsync<CertificateDto>( $"api/certificates/issue/{courseId}?lang={lang}" );

    public Task<byte[]?> DownloadCertificateAsync( Guid certificateId )
        => GetBytesAsync( $"api/certificates/{certificateId}/download" );

    public Task<CertificateVerificationDto?> VerifyAsync( Guid verificationId, string lang = "en" )
        => GetAsync<CertificateVerificationDto>( $"api/certificates/verify/{verificationId}?lang={lang}" );
}
