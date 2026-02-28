using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Pages;

public partial class Billing
{
    [Inject] private ISubscriptionService SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private BillingOverviewDto? _overview;
    private bool _loading = true;
    private bool _cancelling;
    private string? _error;
    private string? _cancelMessage;
    private bool _cancelSuccess;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _overview = await SubscriptionService.GetBillingOverviewAsync();
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
                    // Reload billing overview to reflect the change
                    _overview = await SubscriptionService.GetBillingOverviewAsync();
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
