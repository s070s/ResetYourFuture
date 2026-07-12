using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for subscription-related API operations.
/// </summary>
public interface ISubscriptionConsumer
{
    Task<List<SubscriptionPlanDto>> GetPlansAsync();
    Task<UserSubscriptionStatusDto?> GetStatusAsync();
    Task<CheckoutSessionDto?> CheckoutAsync(Guid planId);
    Task<CancelSubscriptionResultDto?> CancelAsync();
    Task<BillingOverviewDto?> GetBillingOverviewAsync(int page = 1, int pageSize = 10, string sortBy = "createdat", string sortDir = "desc");
}
