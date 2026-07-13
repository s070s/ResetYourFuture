namespace ResetYourFuture.Application.DTOs;

/// <summary>One approved review, as shown to other students on the course page.</summary>
public record CourseReviewDto(
    Guid Id,
    string AuthorName,
    int Rating,
    string Body,
    DateTimeOffset CreatedAt
);

/// <summary>The current user's own review on a course — visible to them regardless of
/// moderation status, so they can see "pending" while waiting and edit it.</summary>
public record MyCourseReviewDto(
    Guid Id,
    int Rating,
    string Body,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

/// <summary>Approved-review summary for course cards/detail — average + count, or null when
/// the course has no approved reviews yet.</summary>
public record CourseRatingSummaryDto(double AverageRating, int ReviewCount);
