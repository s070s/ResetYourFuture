namespace ResetYourFuture.Web.Domain.Enums;

/// <summary>
/// Billing interval for subscription plans.
/// Stored as int for forward compatibility.
/// </summary>
public enum BillingPeriod
{
    Monthly = 1,
    Quarterly = 3,
    Yearly = 12,
    Lifetime = 0
}
