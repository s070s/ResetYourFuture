using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Records a billing event (payment, refund, plan change).
/// Each checkout or plan change creates one record.
/// In production this would mirror Stripe invoice/charge data.
/// </summary>
public class BillingTransaction
{
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the user who was billed.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Navigation: the billed user.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Foreign key to the plan associated with this transaction.
    /// </summary>
    public Guid SubscriptionPlanId { get; set; }

    /// <summary>
    /// Navigation: the plan that was purchased/changed to.
    /// </summary>
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    /// <summary>
    /// Amount charged in base currency (e.g. EUR). Zero for free plan assignments.
    /// Negative for refunds.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code (e.g. "EUR").
    /// </summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Type of transaction.
    /// </summary>
    public BillingTransactionType Type { get; set; }

    /// <summary>
    /// Short human-readable description (e.g. "Upgraded to Pro", "Downgraded to Free").
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Mock Stripe session/payment ID for reference.
    /// </summary>
    public string? StripeSessionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of billing transaction.
/// </summary>
public enum BillingTransactionType
{
    /// <summary>Initial subscription purchase.</summary>
    Purchase = 0,

    /// <summary>Upgrade to a higher tier.</summary>
    Upgrade = 1,

    /// <summary>Downgrade to a lower tier (including Free).</summary>
    Downgrade = 2,

    /// <summary>Recurring renewal charge.</summary>
    Renewal = 3,

    /// <summary>Refund issued.</summary>
    Refund = 4,

    /// <summary>Free plan assignment (no charge).</summary>
    FreePlanAssignment = 5
}
