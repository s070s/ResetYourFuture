namespace ResetYourFuture.Shared.Courses;

/// <summary>
/// Full course detail including modules and progress.
/// </summary>
public record CourseDetailDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsEnrolled,
    bool IsCompleted,
    int CompletedLessons,
    int TotalLessons,
    double ProgressPercent,
    List<ModuleDto> Modules
);

/// <summary>
/// Module within a course.
/// </summary>
public record ModuleDto(
    Guid Id,
    string Title,
    string? Description,
    int SortOrder,
    List<LessonSummaryDto> Lessons
);

/// <summary>
/// Lesson summary for module listing.
/// </summary>
public record LessonSummaryDto(
    Guid Id,
    string Title,
    int ContentType,
    int? DurationMinutes,
    int SortOrder,
    bool IsCompleted
);
