using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class MyCertificates
{
    [Inject] private ICertificateConsumer CertificateConsumer { get; set; } = default!;
    [Inject] private ILogger<MyCertificates> _logger { get; set; } = default!;

    private List<CertificateDto>? _certificates;
    private bool _loading = true;
    private string _error = string.Empty;

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _certificates = await CertificateConsumer.GetMyCertificatesAsync(CurrentLang);
        }
        catch (Exception ex)
        {
            _error = CertificateRes.FailedToLoad;
            _logger.LogError(ex, "Failed to load certificates.");
        }
        finally
        {
            _loading = false;
        }
    }
}
