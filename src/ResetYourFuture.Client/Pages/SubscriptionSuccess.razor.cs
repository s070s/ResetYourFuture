using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.Subscriptions;

namespace ResetYourFuture.Client.Pages;

public partial class SubscriptionSuccess
{
    [Inject] private ISubscriptionService SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private UserSubscriptionStatusDto? _status;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _status = await SubscriptionService.GetStatusAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void GoToCourses() => Navigation.NavigateTo("/courses");
    private void GoToPricing() => Navigation.NavigateTo("/pricing");
}
