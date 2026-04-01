using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the public blog API.
/// </summary>
public class BlogConsumer : IBlogConsumer
{
    private readonly HttpClient _http;

    public BlogConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<IReadOnlyList<BlogArticleSummaryDto>?> GetSummariesAsync(
        int count = 6, string lang = "en", CancellationToken ct = default )
    {
        var response = await _http.GetAsync( $"api/blog/summaries?count={count}&lang={lang}", ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<BlogArticleSummaryDto>>( ct )
            : null;
    }

    public async Task<BlogArticleDto?> GetBySlugAsync(
        string slug, string lang = "en", CancellationToken ct = default )
    {
        var response = await _http.GetAsync( $"api/blog/{slug}?lang={lang}", ct );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BlogArticleDto>( ct )
            : null;
    }
}
