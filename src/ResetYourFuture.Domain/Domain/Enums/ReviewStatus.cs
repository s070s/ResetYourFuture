namespace ResetYourFuture.Domain.Enums;

/// <summary>
/// Moderation state of a <see cref="Entities.CourseReview"/>. Stored as int.
/// </summary>
public enum ReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
