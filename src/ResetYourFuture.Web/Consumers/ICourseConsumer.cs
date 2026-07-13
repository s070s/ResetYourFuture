using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for course-related API operations.
/// </summary>
public interface ICourseConsumer
{
    Task<PagedResult<CourseListItemDto>> GetCoursesAsync(int page = 1, int pageSize = 10, string lang = "en", Guid? categoryId = null, string? search = null);
    Task<CourseDetailDto?> GetCourseAsync(Guid courseId, string lang = "en");
    Task<EnrollmentResultDto?> EnrollAsync(Guid courseId);
    Task<LessonDetailDto?> GetLessonAsync(Guid lessonId, string lang = "en");
    Task<LessonCompletionResultDto?> CompleteLessonAsync(Guid lessonId);
    Task<CourseReviewsResponseDto?> GetReviewsAsync(Guid courseId);
    Task<MyCourseReviewDto?> SaveReviewAsync(Guid courseId, SaveCourseReviewRequest request);
}
