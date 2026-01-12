using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Links a User to a SubscriptionPlan.
/// Tracks subscription lifecycle (start, end, cancellation).
/// A user can have at most one active subscription at a time.
/// </summary>
public class UserSubscription
{
    public Guid Id { get; set; }

    /// <summary>
    /// When the subscription became active.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the subscription period ends (for billing).
    /// Null for lifetime subscriptions.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When the user cancelled. Null if not cancelled.
    /// Subscription remains active until ExpiresAt after cancellation.
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Whether this is the currently active subscription for the user.
    /// Only one subscription per user should have IsActive = true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Foreign key to ApplicationUser
    public required string UserId { get; set; }

    // Navigation: subscriber
    public ApplicationUser User { get; set; } = null!;

    // Foreign key to SubscriptionPlan
    public Guid SubscriptionPlanId { get; set; }

    // Navigation: the plan
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}
