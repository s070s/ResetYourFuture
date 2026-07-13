using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>One retrieved chunk of grounding context, with the human-readable source it came from.</summary>
public record AssistantRetrievedChunk(string Text, string SourceTitle, string SourceUrl);

/// <summary>One search result: a single source (course/lesson/assessment/article), deduplicated
/// from potentially several matching chunks, carrying its best-scoring chunk as a snippet.</summary>
public record AssistantSearchHit(AssistantSourceType SourceType, string Title, string Url, string Snippet);

/// <summary>
/// Ranks the AI assistant's indexed content against a question via cosine similarity and resolves
/// the winning chunks back to their human-readable source (title + site-relative URL).
/// </summary>
public interface IAssistantRetrievalService
{
    /// <summary>One row per matching chunk — grounding context for the assistant's chat prompt.</summary>
    Task<IReadOnlyList<AssistantRetrievedChunk>> SearchAsync(
        string query, string language, int topK, CancellationToken cancellationToken = default);

    /// <summary>One row per matching source (deduplicated across its chunks) — used by site search.</summary>
    Task<IReadOnlyList<AssistantSearchHit>> SearchGroupedAsync(
        string query, string language, int topK, CancellationToken cancellationToken = default);
}
