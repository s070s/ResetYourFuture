using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.Common;

/// <summary>An AssistantContentChunk row projected into memory for cosine-similarity ranking.</summary>
public sealed record CachedAssistantChunk(AssistantSourceType SourceType, Guid SourceId, string Language, string Text, float[] Vector);

/// <summary>
/// In-memory snapshot of all AssistantContentChunk rows. Content volume is small (hundreds of
/// rows), so retrieval ranks this snapshot directly rather than querying a vector index/DB per
/// question. Refreshed by <c>AssistantRetrievalService</c> whenever <see cref="AssistantIndexVersion"/>
/// advances; the swap is a single reference assignment, so readers never see a partial list.
/// </summary>
public sealed class AssistantChunkCache
{
    private IReadOnlyList<CachedAssistantChunk> _chunks = [];
    private int _loadedVersion = -1;

    public IReadOnlyList<CachedAssistantChunk> Chunks => _chunks;

    public bool IsStale(int currentVersion) => _loadedVersion != currentVersion;

    public void Replace(IReadOnlyList<CachedAssistantChunk> chunks, int version)
    {
        _chunks = chunks;
        _loadedVersion = version;
    }
}
