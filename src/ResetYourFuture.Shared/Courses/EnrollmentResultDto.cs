namespace ResetYourFuture.Shared.Courses;

/// <summary>
/// Response for enrollment and completion operations.
/// </summary>
public record EnrollmentResultDto(
    bool Success,
    string? Message,
    Guid? EnrollmentId
);

/// <summary>
/// Response for lesson completion.
/// </summary>
public record LessonCompletionResultDto(
    bool Success,
    string? Message,
    int CompletedLessons,
    int TotalLessons,
    double ProgressPercent,
    bool CourseCompleted
);
