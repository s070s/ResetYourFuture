using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Request record for both create and update of a blog article.
/// TitleEn and SummaryEn are required; El variants are optional and fall back to En when null.
/// </summary>
public record SaveBlogArticleRequest(
    // DQ-3: TitleEn/TitleEl/AuthorName capped to match BlogArticleConfiguration's column
    // lengths — a longer value would pass this check and then throw a SQL truncation error
    // on SaveChanges.
    [Required, MaxLength(200)] string TitleEn,
    [MaxLength(200)] string? TitleEl,
    [Required, MaxLength(200)] string Slug,
    [Required, MaxLength(500)] string SummaryEn,
    [MaxLength(500)] string? SummaryEl,
    [Required] string ContentEn,
    string? ContentEl,
    [MaxLength(500)] string? CoverImageUrl,
    [Required, MaxLength(100)] string AuthorName,
    string[]? Tags,
    bool IsPublished
);
