using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for student-facing and public certificate API operations.
/// </summary>
public class CertificateConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider) : ApiClientBase(http, tokenProvider), ICertificateConsumer
{
    public Task<List<CertificateDto>?> GetMyCertificatesAsync(string lang = "en")
        => GetAsync<List<CertificateDto>>($"api/certificates/my?lang={lang}");

    public Task<CertificateDto?> IssueCertificateAsync(Guid courseId, string lang = "en")
        => PostAsync<CertificateDto>($"api/certificates/issue/{courseId}?lang={lang}");

    // PERF-5: the certificate PDF is downloaded straight from the API over same-origin HTTP
    // (see MyCertificates), so there's no longer a loopback byte-fetch consumer method.

    public Task<CertificateVerificationDto?> VerifyAsync(Guid verificationId, string lang = "en")
        => GetAsync<CertificateVerificationDto>($"api/certificates/verify/{verificationId}?lang={lang}");
}
