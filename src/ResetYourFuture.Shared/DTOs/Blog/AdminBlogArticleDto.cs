namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Admin read DTO — full article with both language variants plus audit fields.
/// </summary>
public record AdminBlogArticleDto(
    Guid Id,
    string TitleEn,
    string? TitleEl,
    string Slug,
    string SummaryEn,
    string? SummaryEl,
    string ContentEn,
    string? ContentEl,
    string? CoverImageUrl,
    string AuthorName,
    string[] Tags,
    DateTimeOffset? PublishedAt,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
