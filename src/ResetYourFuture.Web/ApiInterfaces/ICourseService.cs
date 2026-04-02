using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.ApiInterfaces;

/// <summary>
/// Handles course discovery, enrollment, and lesson consumption for students.
/// </summary>
public interface ICourseService
{
    Task<PagedResult<CourseListItemDto>> GetPublishedCoursesAsync( string userId , int page , int pageSize , string lang );
    Task<CourseDetailDto?> GetCourseDetailAsync( string userId , Guid courseId , string lang );
    Task<ServiceResult<EnrollmentResultDto>> EnrollAsync( string userId , Guid courseId );
    Task<ServiceResult<LessonDetailDto>> GetLessonDetailAsync( string userId , Guid lessonId , string lang );
    Task<ServiceResult<LessonCompletionResultDto>> CompleteLessonAsync( string userId , Guid lessonId );
}
