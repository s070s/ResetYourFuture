namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// A single billing transaction record for display.
/// </summary>
public record BillingTransactionDto(
    Guid Id,
    string PlanName,
    decimal Amount,
    string Currency,
    string Type,
    string Description,
    string? StripeSessionId,
    DateTime CreatedAt
);

/// <summary>
/// Full billing overview for the billing page.
/// </summary>
public record BillingOverviewDto
{
    /// <summary>Current subscription status.</summary>
    public UserSubscriptionStatusDto? CurrentSubscription { get; init; }

    /// <summary>Paged transaction history, newest first.</summary>
    public PagedResult<BillingTransactionDto> Transactions { get; init; } = new( [] , 0 , 1 , 10 );
}

/// <summary>
/// Result of a cancel/downgrade operation.
/// </summary>
public record CancelSubscriptionResultDto(
    bool Success,
    string Message
);
