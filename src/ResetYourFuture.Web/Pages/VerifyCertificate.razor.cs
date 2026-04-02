using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class VerifyCertificate
{
    [Parameter] public Guid VerificationId { get; set; }

    [Inject] private ICertificateConsumer CertificateConsumer { get; set; } = default!;
    [Inject] private ILogger<VerifyCertificate> _logger { get; set; } = default!;

    private CertificateVerificationDto? _result;
    private bool _loading = true;

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _result = await CertificateConsumer.VerifyAsync( VerificationId , CurrentLang );
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex , "Failed to verify certificate {VerificationId}." , VerificationId );
        }
        finally
        {
            _loading = false;
        }
    }
}
