using System.Numerics.Tensors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.ApiServices;

/// <inheritdoc cref="IAssistantRetrievalService"/>
public class AssistantRetrievalService(
    IApplicationDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    AssistantChunkCache cache,
    AssistantIndexVersion version) : IAssistantRetrievalService
{
    private const float MinScore = 0.4f;

    public async Task<IReadOnlyList<AssistantRetrievedChunk>> SearchAsync(
        string query, string language, int topK, CancellationToken cancellationToken = default)
    {
        var ranked = await RankChunksAsync(query, language, topK, cancellationToken);
        if (ranked.Count == 0)
            return [];

        var resolved = await ResolveSourcesAsync(ranked.Select(r => r.Chunk).ToList(), language, cancellationToken);
        return resolved.Select(r => new AssistantRetrievedChunk(r.Chunk.Text, r.Title, r.Url)).ToList();
    }

    /// <summary>
    /// Same ranking core as <see cref="SearchAsync"/>, but deduplicated to one row per source
    /// (its highest-scoring chunk becomes the snippet) — the shape site search needs, since a
    /// document commonly has several matching chunks and a search result list shouldn't repeat it.
    /// </summary>
    public async Task<IReadOnlyList<AssistantSearchHit>> SearchGroupedAsync(
        string query, string language, int topK, CancellationToken cancellationToken = default)
    {
        // Rank more than topK chunks up front since several can collapse into the same source
        // once grouped — otherwise a source with many matching chunks could crowd out others.
        var ranked = await RankChunksAsync(query, language, topK * 4, cancellationToken);
        if (ranked.Count == 0)
            return [];

        var resolved = await ResolveSourcesAsync(ranked.Select(r => r.Chunk).ToList(), language, cancellationToken);
        var scoreByChunk = ranked.ToDictionary(r => r.Chunk, r => r.Score);

        return resolved
            .GroupBy(r => r.Url) // one row per source — URL is the natural dedup key
            .Select(g => g.OrderByDescending(r => scoreByChunk[r.Chunk]).First())
            .OrderByDescending(r => scoreByChunk[r.Chunk])
            .Take(topK)
            .Select(r => new AssistantSearchHit(r.Chunk.SourceType, r.Title, r.Url, r.Chunk.Text))
            .ToList();
    }

    /// <summary>Embeds the query, ranks the cached chunks by cosine similarity, and returns the
    /// top-scoring ones above <see cref="MinScore"/> — the shared core behind both search shapes.</summary>
    private async Task<List<(CachedAssistantChunk Chunk, float Score)>> RankChunksAsync(
        string query, string language, int topK, CancellationToken cancellationToken)
    {
        await RefreshCacheIfStaleAsync(cancellationToken);

        var queryVector = await embeddingGenerator.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        return cache.Chunks
            .Where(c => c.Language == language)
            .Select(c => (Chunk: c, Score: TensorPrimitives.CosineSimilarity(queryVector.Span, c.Vector)))
            .Where(x => x.Score >= MinScore)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    private async Task RefreshCacheIfStaleAsync(CancellationToken cancellationToken)
    {
        var currentVersion = version.Current;
        if (!cache.IsStale(currentVersion))
            return;

        var rows = await db.AssistantContentChunks.AsNoTracking().ToListAsync(cancellationToken);
        var mapped = rows
            .Select(r => new CachedAssistantChunk(r.SourceType, r.SourceId, r.Language, r.Text, EmbeddingCodec.ToFloatArray(r.Embedding)))
            .ToList();
        cache.Replace(mapped, currentVersion);
    }

    /// <summary>
    /// Batches one lookup per source type to resolve each winning chunk's human-readable title and
    /// site-relative URL. A chunk whose source was unpublished/deleted after the last index pass is
    /// silently dropped rather than surfaced with a broken link.
    /// </summary>
    private async Task<List<(CachedAssistantChunk Chunk, string Title, string Url)>> ResolveSourcesAsync(
        List<CachedAssistantChunk> chunks, string language, CancellationToken cancellationToken)
    {
        var isEl = string.Equals(language, "el", StringComparison.OrdinalIgnoreCase);

        var courseIds = chunks.Where(c => c.SourceType == AssistantSourceType.Course).Select(c => c.SourceId).ToHashSet();
        var lessonIds = chunks.Where(c => c.SourceType == AssistantSourceType.Lesson).Select(c => c.SourceId).ToHashSet();
        var assessmentIds = chunks.Where(c => c.SourceType == AssistantSourceType.Assessment).Select(c => c.SourceId).ToHashSet();
        var blogIds = chunks.Where(c => c.SourceType == AssistantSourceType.BlogArticle).Select(c => c.SourceId).ToHashSet();

        var courses = courseIds.Count == 0
            ? []
            : await db.Courses.AsNoTracking().Where(c => courseIds.Contains(c.Id)).ToListAsync(cancellationToken);
        var lessons = lessonIds.Count == 0
            ? []
            : await db.Lessons.AsNoTracking().Include(l => l.Module).Where(l => lessonIds.Contains(l.Id)).ToListAsync(cancellationToken);
        var assessments = assessmentIds.Count == 0
            ? []
            : await db.AssessmentDefinitions.AsNoTracking().Where(a => assessmentIds.Contains(a.Id)).ToListAsync(cancellationToken);
        var blogArticles = blogIds.Count == 0
            ? []
            : await db.BlogArticles.AsNoTracking().Where(b => blogIds.Contains(b.Id)).ToListAsync(cancellationToken);

        var results = new List<(CachedAssistantChunk Chunk, string Title, string Url)>();
        foreach (var chunk in chunks)
        {
            (string Title, string Url)? resolved = chunk.SourceType switch
            {
                AssistantSourceType.Course => courses.Find(c => c.Id == chunk.SourceId) is { } course
                    ? (isEl ? (course.TitleEl ?? course.TitleEn) : course.TitleEn, $"courses/{course.Id}")
                    : null,
                AssistantSourceType.Lesson => lessons.Find(l => l.Id == chunk.SourceId) is { } lesson
                    ? (isEl ? (lesson.TitleEl ?? lesson.TitleEn) : lesson.TitleEn, $"courses/{lesson.Module.CourseId}")
                    : null,
                AssistantSourceType.Assessment => assessments.Find(a => a.Id == chunk.SourceId) is { } assessment
                    ? (isEl ? (assessment.TitleEl ?? assessment.TitleEn) : assessment.TitleEn, $"assessments/{assessment.Id}")
                    : null,
                AssistantSourceType.BlogArticle => blogArticles.Find(b => b.Id == chunk.SourceId) is { } article
                    ? (isEl ? (article.TitleEl ?? article.TitleEn) : article.TitleEn, $"blog/{article.Slug}")
                    : null,
                _ => null
            };

            if (resolved is { } r)
                results.Add((chunk, r.Title, r.Url));
        }

        return results;
    }
}
