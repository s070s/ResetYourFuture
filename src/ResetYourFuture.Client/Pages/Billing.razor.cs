using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Pages;

public partial class Billing
{
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private BillingOverviewDto? _overview;
    private bool _loading = true;
    private bool _cancelling;
    private string? _error;
    private string? _cancelMessage;
    private bool _cancelSuccess;
    private int _page = 1;
    private int _pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];

    protected override async Task OnInitializedAsync()
    {
        await LoadBillingOverviewAsync();
    }

    private async Task LoadBillingOverviewAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _overview = await SubscriptionService.GetBillingOverviewAsync( _page , _pageSize );
        }
        catch ( Exception ex )
        {
            _error = "Failed to load billing information. Please try again.";
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task GoToPage( int page )
    {
        _page = page;
        await LoadBillingOverviewAsync();
    }

    private async Task OnPageSizeChanged( ChangeEventArgs e )
    {
        if ( int.TryParse( e.Value?.ToString() , out var size ) )
        {
            _pageSize = size;
            _page = 1;
            await LoadBillingOverviewAsync();
        }
    }

    private async Task CancelSubscription()
    {
        _cancelling = true;
        _cancelMessage = null;
        try
        {
            var result = await SubscriptionService.CancelAsync();
            if ( result is not null )
            {
                _cancelSuccess = result.Success;
                _cancelMessage = result.Message;

                if ( result.Success )
                {
                    _page = 1;
                    await LoadBillingOverviewAsync();
                }
            }
        }
        catch ( Exception ex )
        {
            _cancelSuccess = false;
            _cancelMessage = "Failed to cancel. Please try again.";
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _cancelling = false;
        }
    }

    private void GoToPricing() => Navigation.NavigateTo( "/pricing" );
}
