using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for student-facing and public certificate API operations.
/// </summary>
public class CertificateConsumer : ICertificateConsumer
{
    private readonly HttpClient _http;

    public CertificateConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<List<CertificateDto>?> GetMyCertificatesAsync( string lang = "en" )
    {
        var response = await _http.GetAsync( $"api/certificates/my?lang={lang}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<CertificateDto>>()
            : null;
    }

    public async Task<CertificateDto?> IssueCertificateAsync( Guid courseId, string lang = "en" )
    {
        var response = await _http.PostAsync( $"api/certificates/issue/{courseId}?lang={lang}", null );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CertificateDto>()
            : null;
    }

    public async Task<byte[]?> DownloadCertificateAsync( Guid certificateId )
    {
        var response = await _http.GetAsync( $"api/certificates/{certificateId}/download" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync()
            : null;
    }

    public async Task<CertificateVerificationDto?> VerifyAsync( Guid verificationId, string lang = "en" )
    {
        var response = await _http.GetAsync( $"api/certificates/verify/{verificationId}?lang={lang}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CertificateVerificationDto>()
            : null;
    }
}
