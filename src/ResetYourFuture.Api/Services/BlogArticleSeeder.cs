using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Shared.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Api.Services;

/// <summary>
/// Seeds example blog articles from BlogSeedData.
/// Idempotent: skips if any articles already exist.
/// </summary>
public static class BlogArticleSeeder
{
    public static async Task SeedAsync( ApplicationDbContext db, ILogger logger )
    {
        if ( await db.BlogArticles.AnyAsync() )
        {
            // If articles exist but none have a Greek title, the data is pre-bilingual.
            // Delete and reseed so bilingual content is populated.
            bool hasBilingualData = await db.BlogArticles.AnyAsync( a => a.TitleEl != null );
            if ( hasBilingualData )
            {
                logger.LogInformation( "Blog articles already seeded. Skipping." );
                return;
            }

            logger.LogInformation( "Detected pre-bilingual blog data — reseeding with bilingual content." );
            db.BlogArticles.RemoveRange( db.BlogArticles );
            await db.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;

        var articles = BlogSeedData.SeedArticles.Select( r => new BlogArticle
        {
            Id            = Guid.NewGuid(),
            TitleEn       = r.TitleEn,
            TitleEl       = r.TitleEl,
            Slug          = r.Slug,
            SummaryEn     = r.SummaryEn,
            SummaryEl     = r.SummaryEl,
            ContentEn     = r.ContentEn,
            ContentEl     = r.ContentEl,
            CoverImageUrl = r.CoverImageUrl,
            AuthorName    = r.AuthorName,
            Tags          = r.Tags is { Length: > 0 }
                                ? JsonSerializer.Serialize( r.Tags )
                                : null,
            IsPublished   = true,
            PublishedAt   = now,
            CreatedAt     = now
        } ).ToList();

        await db.BlogArticles.AddRangeAsync( articles );
        await db.SaveChangesAsync();

        logger.LogInformation( "Seeded {Count} blog articles.", articles.Count );
    }
}
