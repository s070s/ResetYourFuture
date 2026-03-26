using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Shared.Resources;

namespace ResetYourFuture.Client.Pages;

public partial class Pricing
{
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private List<SubscriptionPlanDto> _plans = [];
    private UserSubscriptionStatusDto? _currentStatus;
    private bool _loading = true;
    private bool _processing;
    private string? _error;
    private string? _cancelMessage;
    private bool _cancelSuccess;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _plans = await SubscriptionService.GetPlansAsync();
            _currentStatus = await SubscriptionService.GetStatusAsync();
        }
        catch ( Exception ex )
        {
            _error = PricingRes.FailedToLoadPlans;
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task Checkout( Guid planId )
    {
        _processing = true;
        _cancelMessage = null;
        try
        {
            var session = await SubscriptionService.CheckoutAsync( planId );
            if ( session is not null && !string.IsNullOrEmpty( session.CheckoutUrl ) )
            {
                Navigation.NavigateTo( session.CheckoutUrl , forceLoad: false );
            }
            else
            {
                // Reload status after successful mock checkout
                _currentStatus = await SubscriptionService.GetStatusAsync();
                _error = null;
            }
        }
        catch ( Exception ex )
        {
            _error = PricingRes.CheckoutFailed;
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _processing = false;
        }
    }

    private async Task CancelSubscription()
    {
        _processing = true;
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
                    _currentStatus = await SubscriptionService.GetStatusAsync();
                }
            }
        }
        catch ( Exception ex )
        {
            _cancelSuccess = false;
            _cancelMessage = PricingRes.FailedToCancel;
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _processing = false;
        }
    }

    private void NavigateToRegister()
    {
        Navigation.NavigateTo( "/register" );
    }
}
