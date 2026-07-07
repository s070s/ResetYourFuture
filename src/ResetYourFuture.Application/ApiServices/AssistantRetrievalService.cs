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
        await RefreshCacheIfStaleAsync(cancellationToken);

        var queryVector = await embeddingGenerator.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        var winners = cache.Chunks
            .Where(c => c.Language == language)
            .Select(c => (Chunk: c, Score: TensorPrimitives.CosineSimilarity(queryVector.Span, c.Vector)))
            .Where(x => x.Score >= MinScore)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();

        return winners.Count == 0
            ? []
            : await ResolveSourcesAsync(winners, language, cancellationToken);
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
    private async Task<IReadOnlyList<AssistantRetrievedChunk>> ResolveSourcesAsync(
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

        var results = new List<AssistantRetrievedChunk>();
        foreach (var chunk in chunks)
        {
            var resolved = chunk.SourceType switch
            {
                AssistantSourceType.Course => courses.Find(c => c.Id == chunk.SourceId) is { } course
                    ? new AssistantRetrievedChunk(chunk.Text, isEl ? (course.TitleEl ?? course.TitleEn) : course.TitleEn, $"courses/{course.Id}")
                    : null,
                AssistantSourceType.Lesson => lessons.Find(l => l.Id == chunk.SourceId) is { } lesson
                    ? new AssistantRetrievedChunk(chunk.Text, isEl ? (lesson.TitleEl ?? lesson.TitleEn) : lesson.TitleEn, $"courses/{lesson.Module.CourseId}")
                    : null,
                AssistantSourceType.Assessment => assessments.Find(a => a.Id == chunk.SourceId) is { } assessment
                    ? new AssistantRetrievedChunk(chunk.Text, isEl ? (assessment.TitleEl ?? assessment.TitleEn) : assessment.TitleEn, $"assessments/{assessment.Id}")
                    : null,
                AssistantSourceType.BlogArticle => blogArticles.Find(b => b.Id == chunk.SourceId) is { } article
                    ? new AssistantRetrievedChunk(chunk.Text, isEl ? (article.TitleEl ?? article.TitleEn) : article.TitleEn, $"blog/{article.Slug}")
                    : null,
                _ => null
            };

            if (resolved is not null)
                results.Add(resolved);
        }

        return results;
    }
}
