using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the course API.
/// </summary>
public class CourseConsumer( HttpClient http ) : ApiClientBase( http ), ICourseConsumer
{
    public async Task<PagedResult<CourseListItemDto>> GetCoursesAsync( int page = 1, int pageSize = 10, string lang = "en" )
        => await GetAsync<PagedResult<CourseListItemDto>>( $"api/courses?page={page}&pageSize={pageSize}&lang={lang}" )
           ?? new PagedResult<CourseListItemDto>( [], 0, page, pageSize );

    public Task<CourseDetailDto?> GetCourseAsync( Guid courseId, string lang = "en" )
        => GetAsync<CourseDetailDto>( $"api/courses/{courseId}?lang={lang}" );

    public async Task<EnrollmentResultDto?> EnrollAsync( Guid courseId )
    {
        var response = await Http.PostAsync( $"api/courses/{courseId}/enroll", null );
        if ( response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Forbidden )
            return await response.Content.ReadFromJsonAsync<EnrollmentResultDto>();
        return null;
    }

    public Task<LessonDetailDto?> GetLessonAsync( Guid lessonId, string lang = "en" )
        => GetAsync<LessonDetailDto>( $"api/courses/lessons/{lessonId}?lang={lang}" );

    public Task<LessonCompletionResultDto?> CompleteLessonAsync( Guid lessonId )
        => PostAsync<LessonCompletionResultDto>( $"api/courses/lessons/{lessonId}/complete" );
}
