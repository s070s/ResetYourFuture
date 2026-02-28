using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Interfaces;

/// <summary>
/// Client service interface for subscription-related API operations.
/// </summary>
public interface ISubscriptionService
{
    Task<List<SubscriptionPlanDto>> GetPlansAsync();
    Task<UserSubscriptionStatusDto?> GetStatusAsync();
    Task<CheckoutSessionDto?> CheckoutAsync( Guid planId );
    Task<CancelSubscriptionResultDto?> CancelAsync();
    Task<BillingOverviewDto?> GetBillingOverviewAsync();
}
