namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Request record for both create and update of a blog article.
/// TitleEn and SummaryEn are required; El variants are optional and fall back to En when null.
/// </summary>
public record SaveBlogArticleRequest(
    string TitleEn,
    string? TitleEl,
    string Slug,
    string SummaryEn,
    string? SummaryEl,
    string ContentEn,
    string? ContentEl,
    string? CoverImageUrl,
    string AuthorName,
    string[]? Tags,
    bool IsPublished
);
