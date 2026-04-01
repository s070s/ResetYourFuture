using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin analytics API.
/// </summary>
public class AdminAnalyticsConsumer : IAdminAnalyticsConsumer
{
    private readonly HttpClient _http;

    public AdminAnalyticsConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<AnalyticsSummaryDto?> GetSummaryAsync()
    {
        var response = await _http.GetAsync( "api/admin/analytics/summary" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AnalyticsSummaryDto>()
            : null;
    }
}
