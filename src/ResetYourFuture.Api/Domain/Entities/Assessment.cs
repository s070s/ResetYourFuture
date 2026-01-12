using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Questionnaire-based evaluation. Results stored as JSON for flexibility.
/// Linked to a User; optionally linked to Course or Module for context.
/// </summary>
public class Assessment
{
    public Guid Id { get; set; }

    /// <summary>
    /// Display title (e.g. "Career Interests Assessment").
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Optional description explaining the assessment purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Assessment results stored as JSON.
    /// Schema is flexible to support various questionnaire formats.
    /// </summary>
    public string? ResultsJson { get; set; }

    /// <summary>
    /// Timestamp when the assessment was completed. Null if not yet completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key to ApplicationUser (required)
    public required string UserId { get; set; }

    // Navigation: user who took the assessment
    public ApplicationUser User { get; set; } = null!;

    // Optional foreign key to Course (for context)
    public Guid? CourseId { get; set; }

    // Navigation: optional Course context
    public Course? Course { get; set; }

    // Optional foreign key to Module (for context)
    public Guid? ModuleId { get; set; }

    // Navigation: optional Module context
    public Module? Module { get; set; }
}
