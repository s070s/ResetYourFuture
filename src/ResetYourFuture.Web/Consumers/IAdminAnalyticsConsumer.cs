using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for admin analytics API operations.
/// </summary>
public interface IAdminAnalyticsConsumer
{
    Task<AnalyticsSummaryDto?> GetSummaryAsync();
}
