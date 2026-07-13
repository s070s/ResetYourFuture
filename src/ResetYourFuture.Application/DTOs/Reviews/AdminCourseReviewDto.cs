namespace ResetYourFuture.Application.DTOs;

/// <summary>Admin moderation-queue row — the review plus enough course/author context to judge it.</summary>
public record AdminCourseReviewDto(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    string AuthorName,
    int Rating,
    string Body,
    string Status,
    DateTimeOffset CreatedAt
);
