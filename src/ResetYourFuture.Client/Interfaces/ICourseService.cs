using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Interfaces;

/// <summary>
/// Client service interface for course-related API operations.
/// </summary>
public interface ICourseService
{
    Task<PagedResult<CourseListItemDto>> GetCoursesAsync( int page = 1, int pageSize = 10 );
    Task<CourseDetailDto?> GetCourseAsync( Guid courseId );
    Task<EnrollmentResultDto?> EnrollAsync( Guid courseId );
    Task<LessonDetailDto?> GetLessonAsync( Guid lessonId );
    Task<LessonCompletionResultDto?> CompleteLessonAsync( Guid lessonId );
}
