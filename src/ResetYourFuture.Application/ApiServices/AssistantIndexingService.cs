using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.ApiServices;

using SourceKey = (AssistantSourceType SourceType, Guid SourceId, string Language);

/// <inheritdoc cref="IAssistantIndexingService"/>
public class AssistantIndexingService(
    IApplicationDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<AssistantIndexingService> logger) : IAssistantIndexingService
{
    private static readonly string[] Languages = ["en", "el"];

    public async Task<AssistantIndexSummary> RunIndexPassAsync(CancellationToken cancellationToken = default)
    {
        var existingByKey = (await db.AssistantContentChunks.ToListAsync(cancellationToken))
            .GroupBy(c => (c.SourceType, c.SourceId, c.Language))
            .ToDictionary(g => g.Key, g => g.ToList());

        var touched = new HashSet<SourceKey>();
        var added = 0;
        var updated = 0;
        var unchanged = 0;

        var courses = await db.Courses.AsNoTracking().Where(c => c.IsPublished).ToListAsync(cancellationToken);
        foreach (var course in courses)
            await ProcessSourceAsync(AssistantSourceType.Course, course.Id,
                lang => BuildCourseSource(course, lang), existingByKey, touched,
                r => { if (r) added++; else updated++; }, () => unchanged++, cancellationToken);

        var lessons = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Module).ThenInclude(m => m.Course)
            .Where(l => l.IsPublished && l.Module.Course.IsPublished)
            .ToListAsync(cancellationToken);
        foreach (var lesson in lessons)
            await ProcessSourceAsync(AssistantSourceType.Lesson, lesson.Id,
                lang => BuildLessonSource(lesson, lang), existingByKey, touched,
                r => { if (r) added++; else updated++; }, () => unchanged++, cancellationToken);

        var assessments = await db.AssessmentDefinitions.AsNoTracking().Where(a => a.IsPublished).ToListAsync(cancellationToken);
        foreach (var assessment in assessments)
            await ProcessSourceAsync(AssistantSourceType.Assessment, assessment.Id,
                lang => BuildAssessmentSource(assessment, lang), existingByKey, touched,
                r => { if (r) added++; else updated++; }, () => unchanged++, cancellationToken);

        var articles = await db.BlogArticles.AsNoTracking().Where(a => a.IsPublished).ToListAsync(cancellationToken);
        foreach (var article in articles)
            await ProcessSourceAsync(AssistantSourceType.BlogArticle, article.Id,
                lang => BuildBlogSource(article, lang), existingByKey, touched,
                r => { if (r) added++; else updated++; }, () => unchanged++, cancellationToken);

        var removed = 0;
        foreach (var (key, rows) in existingByKey)
        {
            if (touched.Contains(key))
                continue;

            db.AssistantContentChunks.RemoveRange(rows);
            removed++;
        }

        await db.SaveChangesAsync(cancellationToken);

        var summary = new AssistantIndexSummary(added, updated, removed, unchanged);
        logger.LogInformation(
            "Assistant index pass complete: {Added} added, {Updated} updated, {Removed} removed, {Unchanged} unchanged.",
            summary.Added, summary.Updated, summary.Removed, summary.Unchanged);
        return summary;
    }

    /// <summary>
    /// Diffs one source×language against its stored content hash and, if changed, re-chunks,
    /// re-embeds, and replaces its rows. <paramref name="onWrite"/> is invoked with
    /// <c>isNew: true/false</c> when a write happens; <paramref name="onUnchanged"/> otherwise.
    /// </summary>
    private async Task ProcessSourceAsync(
        AssistantSourceType sourceType,
        Guid sourceId,
        Func<string, (string Header, string Text)?> buildSource,
        Dictionary<SourceKey, List<AssistantContentChunk>> existingByKey,
        HashSet<SourceKey> touched,
        Action<bool> onWrite,
        Action onUnchanged,
        CancellationToken cancellationToken)
    {
        foreach (var lang in Languages)
        {
            var source = buildSource(lang);
            if (source is null)
                continue;

            var key = (sourceType, sourceId, lang);
            touched.Add(key);

            var hash = Sha256Hex(source.Value.Text);
            var isNew = !existingByKey.TryGetValue(key, out var existingRows);
            if (!isNew && existingRows![0].ContentHash == hash)
            {
                onUnchanged();
                continue;
            }

            if (!isNew)
                db.AssistantContentChunks.RemoveRange(existingRows!);

            var stripped = AssistantChunker.StripHtml(source.Value.Text);
            var chunkTexts = AssistantChunker.Chunk(stripped, source.Value.Header);
            if (chunkTexts.Count == 0)
                continue;

            var embeddings = await embeddingGenerator.GenerateAsync(chunkTexts, cancellationToken: cancellationToken);
            var now = DateTime.UtcNow;
            for (var i = 0; i < chunkTexts.Count; i++)
            {
                db.AssistantContentChunks.Add(new AssistantContentChunk
                {
                    SourceType = sourceType,
                    SourceId = sourceId,
                    Language = lang,
                    ChunkIndex = i,
                    Text = chunkTexts[i],
                    Embedding = EmbeddingCodec.ToBytes(embeddings[i].Vector),
                    ContentHash = hash,
                    UpdatedAt = now
                });
            }

            onWrite(isNew);
        }
    }

    private static (string Header, string Text)? BuildCourseSource(Course course, string lang)
    {
        if (lang == "en")
            return ($"Course: {course.TitleEn}", Join(course.TitleEn, course.DescriptionEn));

        if (string.IsNullOrWhiteSpace(course.TitleEl))
            return null;

        return ($"Course: {course.TitleEl}", Join(course.TitleEl, course.DescriptionEl));
    }

    private static (string Header, string Text)? BuildLessonSource(Lesson lesson, string lang)
    {
        if (lang == "en")
        {
            var header = $"Course: {lesson.Module.Course.TitleEn} — Module: {lesson.Module.TitleEn} — Lesson: {lesson.TitleEn}";
            return (header, Join(lesson.TitleEn, lesson.ContentEn));
        }

        if (string.IsNullOrWhiteSpace(lesson.TitleEl))
            return null;

        var courseTitleEl = lesson.Module.Course.TitleEl ?? lesson.Module.Course.TitleEn;
        var moduleTitleEl = lesson.Module.TitleEl ?? lesson.Module.TitleEn;
        var headerEl = $"Course: {courseTitleEl} — Module: {moduleTitleEl} — Lesson: {lesson.TitleEl}";
        return (headerEl, Join(lesson.TitleEl, lesson.ContentEl));
    }

    private static (string Header, string Text)? BuildAssessmentSource(AssessmentDefinition assessment, string lang)
    {
        if (lang == "en")
            return ($"Assessment: {assessment.TitleEn}", Join(assessment.TitleEn, assessment.DescriptionEn));

        if (string.IsNullOrWhiteSpace(assessment.TitleEl))
            return null;

        return ($"Assessment: {assessment.TitleEl}", Join(assessment.TitleEl, assessment.DescriptionEl));
    }

    private static (string Header, string Text)? BuildBlogSource(BlogArticle article, string lang)
    {
        if (lang == "en")
            return ($"Blog: {article.TitleEn}", Join(article.TitleEn, article.SummaryEn, article.ContentEn));

        if (string.IsNullOrWhiteSpace(article.TitleEl))
            return null;

        return ($"Blog: {article.TitleEl}", Join(article.TitleEl, article.SummaryEl, article.ContentEl));
    }

    private static string Join(params string?[] parts) =>
        string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Sha256Hex(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
