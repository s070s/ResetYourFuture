using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class SubscriptionSuccess
{
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<SubscriptionSuccess> _logger { get; set; } = default!;

    private UserSubscriptionStatusDto? _status;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _status = await SubscriptionService.GetStatusAsync();
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex , "Failed to load subscription status on success page." );
        }
        finally
        {
            _loading = false;
        }
    }

    private void GoToCourses() => Navigation.NavigateTo( "/courses" );
    private void GoToPricing() => Navigation.NavigateTo( "/pricing" );
}
