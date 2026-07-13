using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Thin query surface over the assistant's existing embedding index (semantic search across
/// published courses/lessons/assessments/blog articles), falling back to a SQL title LIKE
/// search when the assistant isn't Ready (disabled, Ollama unreachable, or still bootstrapping).
/// </summary>
public interface ISiteSearchService
{
    Task<SiteSearchResultDto> SearchAsync(string query, string language, int limit, CancellationToken cancellationToken = default);
}
