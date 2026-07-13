using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>HTTP consumer for the public learning-path API.</summary>
public class PathConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider) : ApiClientBase(http, tokenProvider), IPathConsumer
{
    public async Task<IReadOnlyList<LearningPathListItemDto>> GetPathsAsync(string lang = "en")
        => await GetAsync<List<LearningPathListItemDto>>($"api/paths?lang={lang}") ?? [];

    public Task<LearningPathDetailDto?> GetPathAsync(Guid id, string lang = "en")
        => GetAsync<LearningPathDetailDto>($"api/paths/{id}?lang={lang}");
}
