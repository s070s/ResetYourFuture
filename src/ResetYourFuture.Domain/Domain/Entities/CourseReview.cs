using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// A student's rating + written review of a course. One per (Course, User) — editable by its
/// author, admin-moderated (Pending → Approved/Rejected) before it's visible to other students.
/// Mirrors the Testimonial moderation flow.
/// </summary>
public class CourseReview
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to Course. Nullable so the relationship is optional at the EF model level —
    /// a soft-deleted course must not silently drop this review out of Include queries.
    /// </summary>
    public Guid? CourseId { get; set; }

    public Course? Course { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public int Rating { get; set; }

    public required string Body { get; set; }

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
