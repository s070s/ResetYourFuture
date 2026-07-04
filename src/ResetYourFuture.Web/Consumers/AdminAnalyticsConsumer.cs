using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin analytics API.
/// </summary>
public class AdminAnalyticsConsumer( HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider ) : ApiClientBase( http, tokenProvider ), IAdminAnalyticsConsumer
{
    public Task<AnalyticsSummaryDto?> GetSummaryAsync()
        => GetAsync<AnalyticsSummaryDto>( "api/admin/analytics/summary" );
}
