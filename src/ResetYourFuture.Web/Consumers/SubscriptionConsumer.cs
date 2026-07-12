using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the subscription API.
/// </summary>
public class SubscriptionConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider) : ApiClientBase(http, tokenProvider), ISubscriptionConsumer
{
    public async Task<List<SubscriptionPlanDto>> GetPlansAsync()
        => await GetAsync<List<SubscriptionPlanDto>>("api/subscriptions/plans") ?? [];

    public Task<UserSubscriptionStatusDto?> GetStatusAsync()
        => GetAsync<UserSubscriptionStatusDto>("api/subscriptions/status");

    public Task<CheckoutSessionDto?> CheckoutAsync(Guid planId)
        => PostJsonAsync<CreateCheckoutRequest, CheckoutSessionDto>(
               "api/subscriptions/checkout", new CreateCheckoutRequest(planId));

    public Task<CancelSubscriptionResultDto?> CancelAsync()
        => PostAsync<CancelSubscriptionResultDto>("api/subscriptions/cancel");

    public Task<BillingOverviewDto?> GetBillingOverviewAsync(int page = 1, int pageSize = 10, string sortBy = "createdat", string sortDir = "desc")
        => GetAsync<BillingOverviewDto>($"api/subscriptions/billing?page={page}&pageSize={pageSize}&sortBy={sortBy}&sortDir={sortDir}");
}
