using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the site search API.
/// </summary>
public class SearchConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider)
    : ApiClientBase(http, tokenProvider), ISearchConsumer
{
    public Task<SiteSearchResultDto?> SearchAsync(string query, int limit, string lang)
        => GetAsync<SiteSearchResultDto>($"api/search?q={Uri.EscapeDataString(query)}&limit={limit}&lang={lang}");
}
