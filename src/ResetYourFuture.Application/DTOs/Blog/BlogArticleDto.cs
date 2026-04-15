namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Full article DTO for the single-article view page.
/// </summary>
public record BlogArticleDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string? CoverImageUrl,
    string AuthorName,
    string[] Tags,
    DateTimeOffset? PublishedAt,
    string Content
);
