namespace ResetYourFuture.Web.Domain.Entities;

/// <summary>
/// Educational unit within a Module. Contains text, PDF, or video content.
/// Belongs to exactly one Module.
/// </summary>
public class Lesson : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Display title for the lesson (English).
    /// </summary>
    public required string TitleEn { get; set; }

    /// <summary>
    /// Display title for the lesson (Greek). Falls back to TitleEn when null.
    /// </summary>
    public string? TitleEl { get; set; }

    /// <summary>
    /// Text or markdown content for the lesson (English).
    /// </summary>
    public string? ContentEn { get; set; }

    /// <summary>
    /// Text or markdown content for the lesson (Greek). Falls back to ContentEn when null.
    /// </summary>
    public string? ContentEl { get; set; }

    /// <summary>
    /// Path to PDF file (if lesson has PDF attachment).
    /// </summary>
    public string? PdfPath { get; set; }

    /// <summary>
    /// Path to video file (if lesson has video).
    /// </summary>
    public string? VideoPath { get; set; }

    /// <summary>
    /// Estimated duration in minutes. Useful for progress tracking.
    /// </summary>
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Determines display order within the parent Module.
    /// </summary>
    public int SortOrder { get; set; }

    // Foreign key to Module
    public Guid ModuleId { get; set; }

    // Navigation: parent Module
    public Module Module { get; set; } = null!;
}
