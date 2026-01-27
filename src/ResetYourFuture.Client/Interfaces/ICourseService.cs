using ResetYourFuture.Shared.Courses;

namespace ResetYourFuture.Client.Interfaces;

/// <summary>
/// Client service interface for course-related API operations.
/// </summary>
public interface ICourseService
{
    Task<List<CourseListItemDto>> GetCoursesAsync();
    Task<CourseDetailDto?> GetCourseAsync(Guid courseId);
    Task<EnrollmentResultDto?> EnrollAsync(Guid courseId);
    Task<LessonDetailDto?> GetLessonAsync(Guid lessonId);
    Task<LessonCompletionResultDto?> CompleteLessonAsync(Guid lessonId);
}
