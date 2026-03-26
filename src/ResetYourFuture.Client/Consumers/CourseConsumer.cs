using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// HTTP consumer for the course API.
/// </summary>
public class CourseConsumer : ICourseConsumer
{
    private readonly HttpClient _http;

    public CourseConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<CourseListItemDto>> GetCoursesAsync( int page = 1, int pageSize = 10, string lang = "en" )
    {
        var response = await _http.GetAsync( $"api/courses?page={page}&pageSize={pageSize}&lang={lang}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<CourseListItemDto>>()
              ?? new PagedResult<CourseListItemDto>( [], 0, page, pageSize )
            : new PagedResult<CourseListItemDto>( [], 0, page, pageSize );
    }

    public async Task<CourseDetailDto?> GetCourseAsync( Guid courseId, string lang = "en" )
    {
        var response = await _http.GetAsync( $"api/courses/{courseId}?lang={lang}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CourseDetailDto>()
            : null;
    }

    public async Task<EnrollmentResultDto?> EnrollAsync( Guid courseId )
    {
        var response = await _http.PostAsync( $"api/courses/{courseId}/enroll", null );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<EnrollmentResultDto>()
            : null;
    }

    public async Task<LessonDetailDto?> GetLessonAsync( Guid lessonId, string lang = "en" )
    {
        var response = await _http.GetAsync( $"api/courses/lessons/{lessonId}?lang={lang}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<LessonDetailDto>()
            : null;
    }

    public async Task<LessonCompletionResultDto?> CompleteLessonAsync( Guid lessonId )
    {
        var response = await _http.PostAsync( $"api/courses/lessons/{lessonId}/complete", null );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<LessonCompletionResultDto>()
            : null;
    }
}
