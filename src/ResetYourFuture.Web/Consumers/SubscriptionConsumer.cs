using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the subscription API.
/// </summary>
public class SubscriptionConsumer : ISubscriptionConsumer
{
    private readonly HttpClient _http;

    public SubscriptionConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<List<SubscriptionPlanDto>> GetPlansAsync()
    {
        var response = await _http.GetAsync( "api/subscription/plans" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<SubscriptionPlanDto>>() ?? []
            : [];
    }

    public async Task<UserSubscriptionStatusDto?> GetStatusAsync()
    {
        var response = await _http.GetAsync( "api/subscription/status" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UserSubscriptionStatusDto>()
            : null;
    }

    public async Task<CheckoutSessionDto?> CheckoutAsync( Guid planId )
    {
        var request = new CreateCheckoutRequest( planId );
        var response = await _http.PostAsJsonAsync( "api/subscription/checkout", request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CheckoutSessionDto>()
            : null;
    }

    public async Task<CancelSubscriptionResultDto?> CancelAsync()
    {
        var response = await _http.PostAsync( "api/subscription/cancel", null );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CancelSubscriptionResultDto>()
            : null;
    }

    public async Task<BillingOverviewDto?> GetBillingOverviewAsync( int page = 1, int pageSize = 10 )
    {
        var response = await _http.GetAsync( $"api/subscription/billing?page={page}&pageSize={pageSize}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BillingOverviewDto>()
            : null;
    }
}
