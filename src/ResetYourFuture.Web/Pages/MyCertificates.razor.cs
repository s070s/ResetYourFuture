using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class MyCertificates
{
    [Inject] private ICertificateConsumer CertificateConsumer { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private List<CertificateDto>? _certificates;
    private bool _loading = true;
    private string _error = string.Empty;
    private Guid? _downloading;

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _certificates = await CertificateConsumer.GetMyCertificatesAsync( CurrentLang );
        }
        catch ( Exception ex )
        {
            _error = "Failed to load certificates.";
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task DownloadAsync( CertificateDto cert )
    {
        _downloading = cert.Id;

        try
        {
            var bytes = await CertificateConsumer.DownloadCertificateAsync( cert.Id );
            if ( bytes is not null )
            {
                var fileName = ToSafeFileName( $"Certificate - {cert.RecipientName} - {cert.CourseTitle}" ) + ".pdf";
                await JSRuntime.InvokeVoidAsync( "downloadFile" , fileName , "application/pdf" , bytes );
            }
            else
            {
                _error = "Certificate PDF is not available.";
            }
        }
        catch ( Exception ex )
        {
            _error = "Download failed. Please try again.";
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _downloading = null;
        }
    }
    private static string ToSafeFileName( string input )
    {
        var safe = string.Concat( input.Select( c => c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' ? '_' : c ) );
        return safe.Length > 100 ? safe[..100].TrimEnd() : safe;
    }
}
