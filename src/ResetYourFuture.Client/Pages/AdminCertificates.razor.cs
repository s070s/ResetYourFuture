using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Pages;

public partial class AdminCertificates
{
    [Inject] private IAdminCertificateConsumer CertificateConsumer { get; set; } = default!;

    private PagedResult<AdminCertificateListItemDto>? _pagedResult;
    private bool _loaded;
    private int _page = 1;
    private int _pageSize = 20;
    private static readonly int[] PageSizeOptions = [10, 20, 50, 100];

    private string _message = string.Empty;
    private string _alertType = "info";
    private Guid? _pendingRevokeId;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _pagedResult = await CertificateConsumer.GetCertificatesAsync( _page , _pageSize );
            if ( _pagedResult is null )
            {
                _message = "Failed to load certificates.";
                _alertType = "danger";
            }
        }
        catch ( Exception ex )
        {
            _message = $"Error loading certificates: {ex.Message}";
            _alertType = "danger";
        }
        finally
        {
            _loaded = true;
        }
    }

    private async Task OnPageSizeChanged( int size )
    {
        _pageSize = size;
        _page = 1;
        await LoadAsync();
    }

    private async Task GoToPage( int page )
    {
        _page = page;
        await LoadAsync();
    }

    private void PromptRevoke( Guid id )
    {
        _pendingRevokeId = id;
    }

    private async Task ExecuteRevokeAsync()
    {
        if ( _pendingRevokeId is null ) return;

        var id = _pendingRevokeId.Value;
        _pendingRevokeId = null;

        if ( await CertificateConsumer.RevokeAsync( id ) )
        {
            _message = "Certificate revoked.";
            _alertType = "success";
            await LoadAsync();
        }
        else
        {
            _message = "Failed to revoke certificate.";
            _alertType = "danger";
        }
    }

    private async Task RegenerateAsync( Guid id )
    {
        if ( await CertificateConsumer.RegenerateAsync( id ) )
        {
            _message = "PDF regenerated successfully.";
            _alertType = "success";
        }
        else
        {
            _message = "Failed to regenerate PDF.";
            _alertType = "danger";
        }
    }
}
