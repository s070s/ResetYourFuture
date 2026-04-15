namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Lightweight card DTO for the landing page blog grid.
/// </summary>
public record BlogArticleSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string? CoverImageUrl,
    string AuthorName,
    string[] Tags,
    DateTimeOffset? PublishedAt
);
