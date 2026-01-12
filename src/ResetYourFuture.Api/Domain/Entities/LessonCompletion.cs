using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Tracks completion of a specific Lesson by a User.
/// Used to calculate course progress.
/// </summary>
public class LessonCompletion
{
    public Guid Id { get; set; }

    /// <summary>
    /// When the lesson was marked as complete.
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Foreign key to ApplicationUser
    public required string UserId { get; set; }

    // Navigation: user who completed the lesson
    public ApplicationUser User { get; set; } = null!;

    // Foreign key to Lesson
    public Guid LessonId { get; set; }

    // Navigation: the completed lesson
    public Lesson Lesson { get; set; } = null!;
}
