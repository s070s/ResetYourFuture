using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Handles course discovery, enrollment, and lesson consumption for students.
/// </summary>
public interface ICourseService
{
    Task<PagedResult<CourseListItemDto>> GetPublishedCoursesAsync(string userId, int page, int pageSize, string lang, CancellationToken cancellationToken = default);
    Task<CourseDetailDto?> GetCourseDetailAsync(string userId, Guid courseId, string lang, CancellationToken cancellationToken = default);
    Task<ServiceResult<EnrollmentResultDto>> EnrollAsync(string userId, Guid courseId, CancellationToken cancellationToken = default);
    Task<ServiceResult<LessonDetailDto>> GetLessonDetailAsync(string userId, Guid lessonId, string lang, CancellationToken cancellationToken = default);
    Task<ServiceResult<LessonCompletionResultDto>> CompleteLessonAsync(string userId, Guid lessonId, CancellationToken cancellationToken = default);
}
