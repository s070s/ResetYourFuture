using ResetYourFuture.Api.Domain.Enums;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Educational unit within a Module. Contains either text or video content.
/// Belongs to exactly one Module.
/// </summary>
public class Lesson
{
    public Guid Id { get; set; }

    /// <summary>
    /// Display title for the lesson.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Type of content: Text or Video.
    /// </summary>
    public ContentType ContentType { get; set; }

    /// <summary>
    /// For Video: URL or external reference.
    /// For Text: rich text / markdown content.
    /// Storage interpretation depends on ContentType.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Estimated duration in minutes. Useful for progress tracking.
    /// </summary>
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Determines display order within the parent Module.
    /// </summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Foreign key to Module
    public Guid ModuleId { get; set; }

    // Navigation: parent Module
    public Module Module { get; set; } = null!;
}
