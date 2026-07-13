using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Course summary for catalog listing.
/// </summary>
public record CourseListItemDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsEnrolled,
    int TotalLessons,
    SubscriptionTier RequiredTier = SubscriptionTier.Free,
    Guid? CategoryId = null,
    string? CategoryName = null,
    double? AverageRating = null,
    int ReviewCount = 0
);
