namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>One retrieved chunk of grounding context, with the human-readable source it came from.</summary>
public record AssistantRetrievedChunk(string Text, string SourceTitle, string SourceUrl);

/// <summary>
/// Ranks the AI assistant's indexed content against a question via cosine similarity and resolves
/// the winning chunks back to their human-readable source (title + site-relative URL).
/// </summary>
public interface IAssistantRetrievalService
{
    Task<IReadOnlyList<AssistantRetrievedChunk>> SearchAsync(
        string query, string language, int topK, CancellationToken cancellationToken = default);
}
