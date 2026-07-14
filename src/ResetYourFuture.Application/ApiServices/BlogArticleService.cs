using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Implements IBlogArticleService using ApplicationDbContext.
/// Public read methods accept a lang param ("en" | "el") and resolve bilingual fields.
/// Admin methods return both En and El variants for editing.
/// </summary>
public class BlogArticleService : IBlogArticleService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<BlogArticleService> _logger;
    private readonly IHtmlSanitizer _sanitizer;

    public BlogArticleService(IApplicationDbContext db, ILogger<BlogArticleService> logger, IHtmlSanitizer sanitizer)
    {
        _db = db;
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public async Task<IReadOnlyList<BlogArticleSummaryDto>> GetPublishedSummariesAsync(
        int count, string lang = "en", CancellationToken cancellationToken = default)
    {
        var isEl = Localized.IsEl(lang);

        // Project only the summary columns in SQL — never the unbounded ContentEn/ContentEl
        // rich-text bodies, which this list discards (PERF-3). Localized.Pick/DeserializeTags
        // can't translate to SQL, so map the trimmed rows in memory.
        var rows = await _db.BlogArticles
            .AsNoTracking()
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.PublishedAt)
            .Take(count)
            .Select(a => new
            {
                a.Id,
                a.TitleEn,
                a.TitleEl,
                a.Slug,
                a.SummaryEn,
                a.SummaryEl,
                a.CoverImageUrl,
                a.AuthorName,
                a.Tags,
                a.PublishedAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(a => new BlogArticleSummaryDto(
            a.Id,
            Localized.Pick(isEl, a.TitleEn, a.TitleEl),
            a.Slug,
            Localized.Pick(isEl, a.SummaryEn, a.SummaryEl),
            a.CoverImageUrl,
            a.AuthorName,
            DeserializeTags(a.Tags),
            a.PublishedAt)).ToList();
    }

    public async Task<BlogArticleDto?> GetPublishedBySlugAsync(
        string slug, string lang = "en", CancellationToken cancellationToken = default)
    {
        var isEl = Localized.IsEl(lang);

        var article = await _db.BlogArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == slug && a.IsPublished, cancellationToken);

        return article is null ? null : ToDto(article, isEl);
    }

    public async Task<PagedResult<AdminBlogArticleDto>> GetAllForAdminAsync(
        int page, int pageSize, string? search, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        var query = _db.BlogArticles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // EF.Functions.Like translates to a sargable LIKE predicate.
            // Explicit ToLower() is dropped — SQL Server's default CI_AS collation
            // makes LIKE case-insensitive without defeating the index.
            var term = $"%{search.Trim()}%";
            query = query.Where(a =>
                EF.Functions.Like(a.TitleEn, term) ||
                (a.TitleEl != null && EF.Functions.Like(a.TitleEl, term)) ||
                EF.Functions.Like(a.Slug, term) ||
                EF.Functions.Like(a.AuthorName, term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminBlogArticleDto>(
            Items: items.Select(ToAdminDto).ToList(),
            TotalCount: total,
            Page: page,
            PageSize: pageSize,
            SortBy: sortBy,
            SortDir: sortDir);
    }

    public async Task<AdminBlogArticleDto?> GetByIdForAdminAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var article = await _db.BlogArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return article is null ? null : ToAdminDto(article);
    }

    public async Task<AdminBlogArticleDto?> CreateAsync(
        SaveBlogArticleRequest request, CancellationToken cancellationToken = default)
    {
        var slugTaken = await _db.BlogArticles
            .AnyAsync(a => a.Slug == request.Slug, cancellationToken);

        if (slugTaken)
        {
            _logger.LogWarning("Blog article create failed: slug '{Slug}' already in use.", request.Slug);
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        var article = new BlogArticle
        {
            Id = Guid.NewGuid(),
            TitleEn = request.TitleEn,
            TitleEl = request.TitleEl,
            Slug = request.Slug,
            SummaryEn = request.SummaryEn,
            SummaryEl = request.SummaryEl,
            ContentEn = _sanitizer.Sanitize(request.ContentEn),
            ContentEl = request.ContentEl is not null ? _sanitizer.Sanitize(request.ContentEl) : null,
            CoverImageUrl = request.CoverImageUrl,
            AuthorName = request.AuthorName,
            Tags = SerializeTags(request.Tags),
            IsPublished = request.IsPublished,
            PublishedAt = request.IsPublished ? now : null,
            CreatedAt = now
        };

        _db.BlogArticles.Add(article);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Blog article created: {Id} '{Title}'.", article.Id, article.TitleEn);
        return ToAdminDto(article);
    }

    public async Task<AdminBlogArticleDto?> UpdateAsync(
        Guid id, SaveBlogArticleRequest request, CancellationToken cancellationToken = default)
    {
        var article = await _db.BlogArticles
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article is null)
            return null;

        var slugTaken = await _db.BlogArticles
            .AnyAsync(a => a.Slug == request.Slug && a.Id != id, cancellationToken);

        if (slugTaken)
        {
            _logger.LogWarning("Blog article update failed: slug '{Slug}' already in use.", request.Slug);
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        article.TitleEn = request.TitleEn;
        article.TitleEl = request.TitleEl;
        article.Slug = request.Slug;
        article.SummaryEn = request.SummaryEn;
        article.SummaryEl = request.SummaryEl;
        article.ContentEn = _sanitizer.Sanitize(request.ContentEn);
        article.ContentEl = request.ContentEl is not null ? _sanitizer.Sanitize(request.ContentEl) : null;
        article.CoverImageUrl = request.CoverImageUrl;
        article.AuthorName = request.AuthorName;
        article.Tags = SerializeTags(request.Tags);
        article.UpdatedAt = now;

        if (request.IsPublished && !article.IsPublished)
        {
            article.IsPublished = true;
            article.PublishedAt ??= now;
        }
        else if (!request.IsPublished && article.IsPublished)
        {
            article.IsPublished = false;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Blog article updated: {Id} '{Title}'.", article.Id, article.TitleEn);
        return ToAdminDto(article);
    }

    public async Task<bool> PublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var article = await _db.BlogArticles
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article is null)
            return false;

        article.IsPublished = true;
        article.PublishedAt ??= DateTimeOffset.UtcNow;
        article.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnpublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var article = await _db.BlogArticles
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article is null)
            return false;

        article.IsPublished = false;
        article.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var article = await _db.BlogArticles
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article is null)
            return false;

        _db.BlogArticles.Remove(article);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Blog article deleted: {Id}.", id);
        return true;
    }

    // --- Helpers ---

    private static string[] DeserializeTags(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static string? SerializeTags(string[]? tags)
        => tags is { Length: > 0 }
            ? JsonSerializer.Serialize(tags)
            : null;

    private static BlogArticleDto ToDto(BlogArticle a, bool isEl) =>
        new(a.Id,
             Localized.Pick(isEl, a.TitleEn, a.TitleEl),
             a.Slug,
             Localized.Pick(isEl, a.SummaryEn, a.SummaryEl),
             a.CoverImageUrl,
             a.AuthorName,
             DeserializeTags(a.Tags),
             a.PublishedAt,
             Localized.Pick(isEl, a.ContentEn, a.ContentEl));

    private static AdminBlogArticleDto ToAdminDto(BlogArticle a) =>
        new(a.Id,
             a.TitleEn, a.TitleEl,
             a.Slug,
             a.SummaryEn, a.SummaryEl,
             a.ContentEn, a.ContentEl,
             a.CoverImageUrl,
             a.AuthorName,
             DeserializeTags(a.Tags),
             a.PublishedAt,
             a.IsPublished,
             a.CreatedAt,
             a.UpdatedAt);
}
