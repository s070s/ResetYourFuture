using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Services;

/// <summary>
/// HTTP implementation of ICourseService.
/// </summary>
public class CourseService : ICourseService
{
    private readonly HttpClient _http;

    public CourseService( HttpClient http )
    {
        _http = http;
    }

    public async Task<List<CourseListItemDto>> GetCoursesAsync()
    {
        var response = await _http.GetAsync( "api/courses" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<List<CourseListItemDto>>() ?? [];
        }
        return [];
    }

    public async Task<CourseDetailDto?> GetCourseAsync( Guid courseId )
    {
        var response = await _http.GetAsync( $"api/courses/{courseId}" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<CourseDetailDto>();
        }
        return null;
    }

    public async Task<EnrollmentResultDto?> EnrollAsync( Guid courseId )
    {
        var response = await _http.PostAsync( $"api/courses/{courseId}/enroll" , null );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<EnrollmentResultDto>();
        }
        return null;
    }

    public async Task<LessonDetailDto?> GetLessonAsync( Guid lessonId )
    {
        var response = await _http.GetAsync( $"api/courses/lessons/{lessonId}" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<LessonDetailDto>();
        }
        return null;
    }

    public async Task<LessonCompletionResultDto?> CompleteLessonAsync( Guid lessonId )
    {
        var response = await _http.PostAsync( $"api/courses/lessons/{lessonId}/complete" , null );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<LessonCompletionResultDto>();
        }
        return null;
    }
}
