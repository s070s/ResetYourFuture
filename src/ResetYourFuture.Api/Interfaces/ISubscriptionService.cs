using ResetYourFuture.Shared.Subscriptions;

namespace ResetYourFuture.Api.Interfaces;

/// <summary>
/// Service for subscription management and entitlement checks.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Get all active subscription plans.
    /// </summary>
    Task<List<SubscriptionPlanDto>> GetPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current user's subscription status.
    /// Returns Free tier if no active subscription exists.
    /// </summary>
    Task<UserSubscriptionStatusDto> GetUserStatusAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current user's subscription tier.
    /// </summary>
    Task<SubscriptionTier> GetUserTierAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a mock checkout session (test mode Stripe stub).
    /// </summary>
    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(string userId, Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign a plan to a user (used by mock checkout / webhook stub).
    /// </summary>
    Task AssignPlanAsync(string userId, Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure the Free plan exists and assign it to a new user.
    /// Called during registration.
    /// </summary>
    Task AssignFreePlanAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel the current paid subscription and revert the user to the Free plan.
    /// </summary>
    Task<CancelSubscriptionResultDto> CancelSubscriptionAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the user's billing overview (current plan + transaction history).
    /// </summary>
    Task<BillingOverviewDto> GetBillingOverviewAsync(string userId, CancellationToken cancellationToken = default);
}
