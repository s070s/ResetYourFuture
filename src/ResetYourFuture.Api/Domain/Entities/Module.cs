namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Groups Lessons within a Course. Ordered via SortOrder.
/// Belongs to exactly one Course.
/// </summary>
public class Module
{
    public Guid Id { get; set; }

    /// <summary>
    /// Display title for the module.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Optional summary of module content.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Determines display order within the parent Course.
    /// </summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Foreign key to Course
    public Guid CourseId { get; set; }

    // Navigation: parent Course
    public Course Course { get; set; } = null!;

    // Navigation: one Module has many Lessons
    public ICollection<Lesson> Lessons { get; set; } = [];
}
