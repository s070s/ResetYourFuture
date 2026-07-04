using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the public blog API.
/// </summary>
public class BlogConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider) : ApiClientBase(http, tokenProvider), IBlogConsumer
{
    public Task<IReadOnlyList<BlogArticleSummaryDto>?> GetSummariesAsync(
        int count = 6, string lang = "en", CancellationToken ct = default)
        => GetAsync<IReadOnlyList<BlogArticleSummaryDto>>($"api/blog/summaries?count={count}&lang={lang}", ct);

    public Task<BlogArticleDto?> GetBySlugAsync(
        string slug, string lang = "en", CancellationToken ct = default)
        => GetAsync<BlogArticleDto>($"api/blog/{slug}?lang={lang}", ct);
}
