using ResetYourFuture.Shared.Subscriptions;

namespace ResetYourFuture.Shared.Courses;

/// <summary>
/// Course summary for catalog listing.
/// </summary>
public record CourseListItemDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsEnrolled,
    int TotalLessons,
    SubscriptionTier RequiredTier = SubscriptionTier.Free
);
