using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Implements IBlogArticleService using ApplicationDbContext.
/// Public read methods accept a lang param ("en" | "el") and resolve bilingual fields.
/// Admin methods return both En and El variants for editing.
/// </summary>
public class BlogArticleService : IBlogArticleService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<BlogArticleService> _logger;
    private readonly IHtmlSanitizer _sanitizer;

    public BlogArticleService( ApplicationDbContext db, ILogger<BlogArticleService> logger, IHtmlSanitizer sanitizer )
    {
        _db = db;
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public async Task<IReadOnlyList<BlogArticleSummaryDto>> GetPublishedSummariesAsync(
        int count, string lang = "en", CancellationToken cancellationToken = default )
    {
        var isEl = IsEl( lang );

        var articles = await _db.BlogArticles
            .AsNoTracking()
            .Where( a => a.IsPublished )
            .OrderByDescending( a => a.PublishedAt )
            .Take( count )
            .ToListAsync( cancellationToken );

        return articles.Select( a => ToSummaryDto( a, isEl ) ).ToList();
    }

    public async Task<BlogArticleDto?> GetPublishedBySlugAsync(
        string slug, string lang = "en", CancellationToken cancellationToken = default )
    {
        var isEl = IsEl( lang );

        var article = await _db.BlogArticles
            .AsNoTracking()
            .FirstOrDefaultAsync( a => a.Slug == slug && a.IsPublished, cancellationToken );

        return article is null ? null : ToDto( article, isEl );
    }

    public async Task<PagedResult<AdminBlogArticleDto>> GetAllForAdminAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken = default )
    {
        page = Math.Max( 1, page );
        pageSize = Math.Clamp( pageSize, 1, 100 );

        var query = _db.BlogArticles.AsNoTracking();

        if ( !string.IsNullOrWhiteSpace( search ) )
        {
            var term = search.Trim().ToLower();
            query = query.Where( a =>
                a.TitleEn.ToLower().Contains( term ) ||
                ( a.TitleEl != null && a.TitleEl.ToLower().Contains( term ) ) ||
                a.Slug.ToLower().Contains( term ) ||
                a.AuthorName.ToLower().Contains( term ) );
        }

        var total = await query.CountAsync( cancellationToken );

        var items = await query
            .OrderByDescending( a => a.CreatedAt )
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .ToListAsync( cancellationToken );

        return new PagedResult<AdminBlogArticleDto>(
            Items: items.Select( ToAdminDto ).ToList(),
            TotalCount: total,
            Page: page,
            PageSize: pageSize,
            SortBy: "createdAt",
            SortDir: "desc" );
    }

    public async Task<AdminBlogArticleDto?> GetByIdForAdminAsync(
        Guid id, CancellationToken cancellationToken = default )
    {
        var article = await _db.BlogArticles
            .AsNoTracking()
            .FirstOrDefaultAsync( a => a.Id == id, cancellationToken );

        return article is null ? null : ToAdminDto( article );
    }

    public async Task<AdminBlogArticleDto?> CreateAsync(
        SaveBlogArticleRequest request, CancellationToken cancellationToken = default )
    {
        var slugTaken = await _db.BlogArticles
            .AnyAsync( a => a.Slug == request.Slug, cancellationToken );

        if ( slugTaken )
        {
            _logger.LogWarning( "Blog article create failed: slug '{Slug}' already in use.", request.Slug );
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        var article = new BlogArticle
        {
            Id            = Guid.NewGuid(),
            TitleEn       = request.TitleEn,
            TitleEl       = request.TitleEl,
            Slug          = request.Slug,
            SummaryEn     = request.SummaryEn,
            SummaryEl     = request.SummaryEl,
            ContentEn     = _sanitizer.Sanitize( request.ContentEn ),
            ContentEl     = request.ContentEl is not null ? _sanitizer.Sanitize( request.ContentEl ) : null,
            CoverImageUrl = request.CoverImageUrl,
            AuthorName    = request.AuthorName,
            Tags          = SerializeTags( request.Tags ),
            IsPublished   = request.IsPublished,
            PublishedAt   = request.IsPublished ? now : null,
            CreatedAt     = now
        };

        _db.BlogArticles.Add( article );
        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation( "Blog article created: {Id} '{Title}'.", article.Id, article.TitleEn );
        return ToAdminDto( article );
    }

    public async Task<AdminBlogArticleDto?> UpdateAsync(
        Guid id, SaveBlogArticleRequest request, CancellationToken cancellationToken = default )
    {
        var article = await _db.BlogArticles
            .FirstOrDefaultAsync( a => a.Id == id, cancellationToken );

        if ( article is null )
            return null;

        var slugTaken = await _db.BlogArticles
            .AnyAsync( a => a.Slug == request.Slug && a.Id != id, cancellationToken );

        if ( slugTaken )
        {
            _logger.LogWarning( "Blog article update failed: slug '{Slug}' already in use.", request.Slug );
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        article.TitleEn       = request.TitleEn;
        article.TitleEl       = request.TitleEl;
        article.Slug          = request.Slug;
        article.SummaryEn     = request.SummaryEn;
        article.SummaryEl     = request.SummaryEl;
        article.ContentEn     = _sanitizer.Sanitize( request.ContentEn );
        article.ContentEl     = request.ContentEl is not null ? _sanitizer.Sanitize( request.ContentEl ) : null;
        article.CoverImageUrl = request.CoverImageUrl;
        article.AuthorName    = request.AuthorName;
        article.Tags          = SerializeTags( request.Tags );
        article.UpdatedAt     = now;

        if ( request.IsPublished && !article.IsPublished )
        {
            article.IsPublished = true;
            article.PublishedAt ??= now;
        }
        else if ( !request.IsPublished && article.IsPublished )
        {
            article.IsPublished = false;
        }

        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation( "Blog article updated: {Id} '{Title}'.", article.Id, article.TitleEn );
        return ToAdminDto( article );
    }

    public async Task<bool> PublishAsync( Guid id, CancellationToken cancellationToken = default )
    {
        var article = await _db.BlogArticles
            .FirstOrDefaultAsync( a => a.Id == id, cancellationToken );

        if ( article is null )
            return false;

        article.IsPublished = true;
        article.PublishedAt ??= DateTimeOffset.UtcNow;
        article.UpdatedAt   = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );
        return true;
    }

    public async Task<bool> UnpublishAsync( Guid id, CancellationToken cancellationToken = default )
    {
        var article = await _db.BlogArticles
            .FirstOrDefaultAsync( a => a.Id == id, cancellationToken );

        if ( article is null )
            return false;

        article.IsPublished = false;
        article.UpdatedAt   = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );
        return true;
    }

    public async Task<bool> DeleteAsync( Guid id, CancellationToken cancellationToken = default )
    {
        var article = await _db.BlogArticles
            .FirstOrDefaultAsync( a => a.Id == id, cancellationToken );

        if ( article is null )
            return false;

        _db.BlogArticles.Remove( article );
        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation( "Blog article deleted: {Id}.", id );
        return true;
    }

    // --- Helpers ---

    private static bool IsEl( string lang ) =>
        string.Equals( lang, "el", StringComparison.OrdinalIgnoreCase );

    private static string[] DeserializeTags( string? json )
        => string.IsNullOrWhiteSpace( json )
            ? []
            : JsonSerializer.Deserialize<string[]>( json ) ?? [];

    private static string? SerializeTags( string[]? tags )
        => tags is { Length: > 0 }
            ? JsonSerializer.Serialize( tags )
            : null;

    private static BlogArticleSummaryDto ToSummaryDto( BlogArticle a, bool isEl ) =>
        new( a.Id,
             isEl ? ( a.TitleEl   ?? a.TitleEn )   : a.TitleEn,
             a.Slug,
             isEl ? ( a.SummaryEl ?? a.SummaryEn ) : a.SummaryEn,
             a.CoverImageUrl,
             a.AuthorName,
             DeserializeTags( a.Tags ),
             a.PublishedAt );

    private static BlogArticleDto ToDto( BlogArticle a, bool isEl ) =>
        new( a.Id,
             isEl ? ( a.TitleEl   ?? a.TitleEn )   : a.TitleEn,
             a.Slug,
             isEl ? ( a.SummaryEl ?? a.SummaryEn ) : a.SummaryEn,
             a.CoverImageUrl,
             a.AuthorName,
             DeserializeTags( a.Tags ),
             a.PublishedAt,
             isEl ? ( a.ContentEl ?? a.ContentEn ) : a.ContentEn );

    private static AdminBlogArticleDto ToAdminDto( BlogArticle a ) =>
        new( a.Id,
             a.TitleEn, a.TitleEl,
             a.Slug,
             a.SummaryEn, a.SummaryEl,
             a.ContentEn, a.ContentEl,
             a.CoverImageUrl,
             a.AuthorName,
             DeserializeTags( a.Tags ),
             a.PublishedAt,
             a.IsPublished,
             a.CreatedAt,
             a.UpdatedAt );
}
