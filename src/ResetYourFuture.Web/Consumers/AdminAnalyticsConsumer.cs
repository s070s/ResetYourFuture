using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin analytics API.
/// </summary>
public class AdminAnalyticsConsumer( HttpClient http ) : ApiClientBase( http ), IAdminAnalyticsConsumer
{
    public Task<AnalyticsSummaryDto?> GetSummaryAsync()
        => GetAsync<AnalyticsSummaryDto>( "api/admin/analytics/summary" );
}
