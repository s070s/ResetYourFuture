using ResetYourFuture.Api.Domain.Enums;
using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Represents a User's participation in a Course.
/// Tracks enrollment date, completion status, and optional progress metadata.
/// </summary>
public class Enrollment
{
    public Guid Id { get; set; }

    /// <summary>
    /// When the user enrolled in the course.
    /// </summary>
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current status of the enrollment.
    /// </summary>
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    /// <summary>
    /// When the enrollment was completed. Null if not yet completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Optional JSON for flexible progress tracking (e.g. completed lessons, bookmarks).
    /// </summary>
    public string? ProgressJson { get; set; }

    // Foreign key to ApplicationUser
    public required string UserId { get; set; }

    // Navigation: enrolled user
    public ApplicationUser User { get; set; } = null!;

    // Foreign key to Course
    public Guid CourseId { get; set; }

    // Navigation: enrolled course
    public Course Course { get; set; } = null!;
}
