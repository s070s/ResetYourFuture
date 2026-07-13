namespace ResetYourFuture.Application.DTOs;

/// <summary>Everything CourseDetail needs about reviews in one round trip: the approved list,
/// the aggregate rating, and (when authenticated) the caller's own review at any status.</summary>
public record CourseReviewsResponseDto(
    List<CourseReviewDto> Reviews,
    CourseRatingSummaryDto? Summary,
    MyCourseReviewDto? MyReview
);
