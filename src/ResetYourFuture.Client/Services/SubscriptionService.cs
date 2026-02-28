using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Services;

/// <summary>
/// HTTP implementation of ISubscriptionService.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly HttpClient _http;

    public SubscriptionService( HttpClient http )
    {
        _http = http;
    }

    public async Task<List<SubscriptionPlanDto>> GetPlansAsync()
    {
        var response = await _http.GetAsync( "api/subscription/plans" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<List<SubscriptionPlanDto>>() ?? [];
        }
        return [];
    }

    public async Task<UserSubscriptionStatusDto?> GetStatusAsync()
    {
        var response = await _http.GetAsync( "api/subscription/status" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<UserSubscriptionStatusDto>();
        }
        return null;
    }

    public async Task<CheckoutSessionDto?> CheckoutAsync( Guid planId )
    {
        var request = new CreateCheckoutRequest( planId );
        var response = await _http.PostAsJsonAsync( "api/subscription/checkout" , request );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<CheckoutSessionDto>();
        }
        return null;
    }

    public async Task<CancelSubscriptionResultDto?> CancelAsync()
    {
        var response = await _http.PostAsync( "api/subscription/cancel" , null );
        return await response.Content.ReadFromJsonAsync<CancelSubscriptionResultDto>();
    }

    public async Task<BillingOverviewDto?> GetBillingOverviewAsync()
    {
        var response = await _http.GetAsync( "api/subscription/billing" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<BillingOverviewDto>();
        }
        return null;
    }
}
