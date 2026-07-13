using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.ApiServices;

/// <inheritdoc cref="ISiteSearchService"/>
public class SiteSearchService(
    IApplicationDbContext db,
    IAssistantRetrievalService retrieval,
    AssistantRuntimeState assistantState,
    ILogger<SiteSearchService> logger) : ISiteSearchService
{
    public async Task<SiteSearchResultDto> SearchAsync(
        string query, string language, int limit, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length == 0)
            return new SiteSearchResultDto([], SemanticSearchUsed: false);

        // Only attempt semantic search once the assistant is actually Ready — before that the
        // chunk cache is empty (Disabled/OllamaUnreachable/DownloadingModels), so a title search
        // is both faster and the only one that can find anything.
        if (assistantState.Status == AssistantAvailability.Ready)
        {
            try
            {
                var hits = await retrieval.SearchGroupedAsync(query, language, limit, cancellationToken);
                if (hits.Count > 0)
                    return new SiteSearchResultDto(ToHitDtos(hits), SemanticSearchUsed: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SiteSearchService: semantic search failed — falling back to title search.");
            }
        }

        return new SiteSearchResultDto(await TitleSearchAsync(query, language, limit, cancellationToken), SemanticSearchUsed: false);
    }

    private static List<SiteSearchHitDto> ToHitDtos(IReadOnlyList<AssistantSearchHit> hits) =>
        hits.Select(h => new SiteSearchHitDto(h.SourceType.ToString(), h.Title, Truncate(h.Snippet), h.Url)).ToList();

    private async Task<List<SiteSearchHitDto>> TitleSearchAsync(
        string query, string language, int limit, CancellationToken cancellationToken)
    {
        var isEl = string.Equals(language, "el", StringComparison.OrdinalIgnoreCase);
        var perType = Math.Max(1, limit / 3 + 1); // spread the cap roughly evenly across the three sources

        // Plain .Contains (not EF.Functions.Like): translates to a sargable LIKE '%term%' on SQL
        // Server under its default case-insensitive collation, and — unlike EF.Functions.Like —
        // also runs on the InMemory provider the Web.Tests factory uses.
        var courses = await db.Courses.AsNoTracking()
            .Where(c => c.IsPublished && (c.TitleEn.Contains(query) || (c.TitleEl != null && c.TitleEl.Contains(query))))
            .OrderBy(c => c.TitleEn)
            .Take(perType)
            .Select(c => new SiteSearchHitDto("Course", isEl ? (c.TitleEl ?? c.TitleEn) : c.TitleEn, null, $"courses/{c.Id}"))
            .ToListAsync(cancellationToken);

        var assessments = await db.AssessmentDefinitions.AsNoTracking()
            .Where(a => a.IsPublished && (a.TitleEn.Contains(query) || (a.TitleEl != null && a.TitleEl.Contains(query))))
            .OrderBy(a => a.TitleEn)
            .Take(perType)
            .Select(a => new SiteSearchHitDto("Assessment", isEl ? (a.TitleEl ?? a.TitleEn) : a.TitleEn, null, $"assessments/{a.Id}"))
            .ToListAsync(cancellationToken);

        var articles = await db.BlogArticles.AsNoTracking()
            .Where(a => a.IsPublished && (a.TitleEn.Contains(query) || (a.TitleEl != null && a.TitleEl.Contains(query))))
            .OrderBy(a => a.TitleEn)
            .Take(perType)
            .Select(a => new SiteSearchHitDto("BlogArticle", isEl ? (a.TitleEl ?? a.TitleEn) : a.TitleEn, null, $"blog/{a.Slug}"))
            .ToListAsync(cancellationToken);

        return courses.Concat(assessments).Concat(articles).Take(limit).ToList();
    }

    private const int MaxSnippetLength = 160;

    private static string Truncate(string text) =>
        text.Length <= MaxSnippetLength ? text : text[..MaxSnippetLength] + "…";
}
