namespace ResetYourFuture.Web.Domain.Entities;

/// <summary>
/// Represents a blog article published on the public landing page.
/// Bilingual content: En fields are required; El fields fall back to En when null.
/// Inherits audit tracking (CreatedAt, UpdatedAt, IsPublished, PublishedAt) from AuditableEntity.
/// </summary>
public class BlogArticle : AuditableEntity
{
    public Guid Id { get; set; }

    public required string TitleEn { get; set; }
    public string? TitleEl { get; set; }

    public required string Slug { get; set; }

    public required string SummaryEn { get; set; }
    public string? SummaryEl { get; set; }

    public required string ContentEn { get; set; }
    public string? ContentEl { get; set; }

    public string? CoverImageUrl { get; set; }

    public required string AuthorName { get; set; }

    /// <summary>JSON-serialized string[].</summary>
    public string? Tags { get; set; }
}
