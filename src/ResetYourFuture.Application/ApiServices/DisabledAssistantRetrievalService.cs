using ResetYourFuture.Application.ApiInterfaces;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// No-op <see cref="IAssistantRetrievalService"/> registered when Assistant:Enabled is false, so
/// consumers that only need semantic search on a best-effort basis — <c>SiteSearchService</c> —
/// can depend on the interface unconditionally and fall back (e.g. to a title LIKE search)
/// instead of failing to resolve or requiring their own Ollama dependency.
/// </summary>
public class DisabledAssistantRetrievalService : IAssistantRetrievalService
{
    public Task<IReadOnlyList<AssistantRetrievedChunk>> SearchAsync(
        string query, string language, int topK, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AssistantRetrievedChunk>>([]);

    public Task<IReadOnlyList<AssistantSearchHit>> SearchGroupedAsync(
        string query, string language, int topK, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AssistantSearchHit>>([]);
}
