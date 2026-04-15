  
namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Course summary for catalog listing.
/// </summary>
public record CourseListItemDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsEnrolled,
    int TotalLessons,
    SubscriptionTierEnum RequiredTier = SubscriptionTierEnum.Free
);
