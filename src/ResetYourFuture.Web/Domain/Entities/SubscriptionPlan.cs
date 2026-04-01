using ResetYourFuture.Web.Domain.Enums;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Domain.Entities;

/// <summary>
/// Defines commercial access tiers (Free / Plus / Pro).
/// Not tied to Identity roles. Defines pricing and feature limits.
/// </summary>
public class SubscriptionPlan
{
    public Guid Id
    {
        get; set;
    }

    /// <summary>
    /// Display name (e.g. "Free", "Plus", "Pro").
    /// </summary>
    public required string Name
    {
        get; set;
    }

    /// <summary>
    /// Optional marketing description.
    /// </summary>
    public string? Description
    {
        get; set;
    }

    /// <summary>
    /// Price in base currency units (e.g. EUR cents or smallest unit).
    /// Zero for free tier.
    /// </summary>
    public decimal Price
    {
        get; set;
    }

    /// <summary>
    /// Billing interval for the plan.
    /// </summary>
    public BillingPeriod BillingPeriod
    {
        get; set;
    }

    /// <summary>
    /// The subscription tier this plan belongs to.
    /// </summary>
    public SubscriptionTierEnum Tier { get; set; } = SubscriptionTierEnum.Free;

    /// <summary>
    /// JSON for flexible feature flags/limits (e.g. max courses, downloads, etc.).
    /// </summary>
    public string? FeaturesJson
    {
        get; set;
    }

    /// <summary>
    /// Whether this plan is currently available for new subscribers.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt
    {
        get; set;
    }

    // Navigation: users subscribed to this plan
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = [];
}
