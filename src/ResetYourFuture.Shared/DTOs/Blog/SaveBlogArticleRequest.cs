using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Request record for both create and update of a blog article.
/// TitleEn and SummaryEn are required; El variants are optional and fall back to En when null.
/// </summary>
public record SaveBlogArticleRequest(
    [Required, MaxLength(300)] string TitleEn,
    [MaxLength(300)] string? TitleEl,
    [Required, MaxLength(200)] string Slug,
    [Required, MaxLength(500)] string SummaryEn,
    [MaxLength(500)] string? SummaryEl,
    [Required] string ContentEn,
    string? ContentEl,
    [MaxLength(500)] string? CoverImageUrl,
    [Required, MaxLength(200)] string AuthorName,
    string[]? Tags,
    bool IsPublished
);
