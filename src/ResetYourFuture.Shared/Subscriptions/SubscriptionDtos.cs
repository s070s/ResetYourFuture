namespace ResetYourFuture.Shared.Subscriptions;

/// <summary>
/// Available subscription plan for display in pricing table.
/// </summary>
public record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string BillingPeriod,
    SubscriptionTier Tier,
    PlanFeaturesDto? Features,
    bool IsActive
);

/// <summary>
/// Feature limits/flags for a subscription plan.
/// </summary>
public record PlanFeaturesDto
{
    public int MaxCourses { get; init; }
    public bool AssessmentAccess { get; init; }
    public bool CertificateAccess { get; init; }
    public bool PrioritySupport { get; init; }
}

/// <summary>
/// Current user's subscription status.
/// </summary>
public record UserSubscriptionStatusDto(
    SubscriptionTier Tier,
    string PlanName,
    DateTime StartedAt,
    DateTime? ExpiresAt,
    bool IsActive,
    PlanFeaturesDto? Features
);

/// <summary>
/// Result of a checkout session creation.
/// </summary>
public record CheckoutSessionDto(
    string SessionId,
    string? CheckoutUrl,
    string Status
);

/// <summary>
/// Request to create a checkout session for a plan.
/// </summary>
public record CreateCheckoutRequest(
    Guid PlanId
);
