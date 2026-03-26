namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Groups Lessons within a Course. Ordered via SortOrder.
/// Belongs to exactly one Course.
/// </summary>
public class Module : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Display title for the module (English).
    /// </summary>
    public required string TitleEn { get; set; }

    /// <summary>
    /// Display title for the module (Greek). Falls back to TitleEn when null.
    /// </summary>
    public string? TitleEl { get; set; }

    /// <summary>
    /// Optional summary of module content (English).
    /// </summary>
    public string? DescriptionEn { get; set; }

    /// <summary>
    /// Optional summary of module content (Greek). Falls back to DescriptionEn when null.
    /// </summary>
    public string? DescriptionEl { get; set; }

    /// <summary>
    /// Determines display order within the parent Course.
    /// </summary>
    public int SortOrder { get; set; }

    // Foreign key to Course
    public Guid CourseId { get; set; }

    // Navigation: parent Course
    public Course Course { get; set; } = null!;

    // Navigation: one Module has many Lessons
    public ICollection<Lesson> Lessons { get; set; } = [];
}
