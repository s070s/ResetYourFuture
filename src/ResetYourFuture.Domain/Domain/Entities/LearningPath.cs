namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// An ordered sequence of courses ("Career Change Starter → CV Lab → Interview Mastery").
/// Publish workflow reuses <see cref="AuditableEntity"/> (IsPublished), same as Course.
/// </summary>
public class LearningPath : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string TitleEn { get; set; }

    public string? TitleEl { get; set; }

    public string? DescriptionEn { get; set; }

    public string? DescriptionEl { get; set; }

    public Guid? CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>Controls display order on the /paths catalog page. Lower = first.</summary>
    public int DisplayOrder { get; set; }

    public ICollection<LearningPathStep> Steps { get; set; } = [];
}
