namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>Outcome of one <see cref="IAssistantIndexingService.RunIndexPassAsync"/> call, for logging.</summary>
public record AssistantIndexSummary(int Added, int Updated, int Removed, int Unchanged);

/// <summary>
/// Re-indexes published Courses/Lessons/Assessments/BlogArticles into <c>AssistantContentChunks</c>
/// for the AI assistant's retrieval-augmented answers. Re-embeds only sources whose text changed
/// since the last pass (content-hash diff), and removes chunks for sources that are gone or
/// unpublished.
/// </summary>
public interface IAssistantIndexingService
{
    Task<AssistantIndexSummary> RunIndexPassAsync(CancellationToken cancellationToken = default);
}
