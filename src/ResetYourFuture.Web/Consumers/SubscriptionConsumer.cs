using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the subscription API.
/// </summary>
public class SubscriptionConsumer( HttpClient http ) : ApiClientBase( http ), ISubscriptionConsumer
{
    public async Task<List<SubscriptionPlanDto>> GetPlansAsync()
        => await GetAsync<List<SubscriptionPlanDto>>( "api/subscription/plans" ) ?? [];

    public Task<UserSubscriptionStatusDto?> GetStatusAsync()
        => GetAsync<UserSubscriptionStatusDto>( "api/subscription/status" );

    public Task<CheckoutSessionDto?> CheckoutAsync( Guid planId )
        => PostJsonAsync<CreateCheckoutRequest, CheckoutSessionDto>(
               "api/subscription/checkout", new CreateCheckoutRequest( planId ) );

    public Task<CancelSubscriptionResultDto?> CancelAsync()
        => PostAsync<CancelSubscriptionResultDto>( "api/subscription/cancel" );

    public Task<BillingOverviewDto?> GetBillingOverviewAsync( int page = 1, int pageSize = 10 )
        => GetAsync<BillingOverviewDto>( $"api/subscription/billing?page={page}&pageSize={pageSize}" );
}
