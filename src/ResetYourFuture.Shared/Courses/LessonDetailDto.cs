namespace ResetYourFuture.Shared.Courses;

/// <summary>
/// Full lesson detail for the lesson viewer.
/// </summary>
public record LessonDetailDto(
    Guid Id,
    string Title,
    int ContentType,
    string? Content,
    int? DurationMinutes,
    bool IsCompleted,
    Guid ModuleId,
    string ModuleTitle,
    Guid CourseId,
    string CourseTitle,
    Guid? PreviousLessonId,
    Guid? NextLessonId
);
