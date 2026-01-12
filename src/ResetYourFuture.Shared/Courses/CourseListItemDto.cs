namespace ResetYourFuture.Shared.Courses;

/// <summary>
/// Course summary for catalog listing.
/// </summary>
public record CourseListItemDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsEnrolled,
    int TotalLessons
);
