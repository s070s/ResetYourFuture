using ResetYourFuture.Application.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the course API.
/// </summary>
public class CourseConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider) : ApiClientBase(http, tokenProvider), ICourseConsumer
{
    public async Task<PagedResult<CourseListItemDto>> GetCoursesAsync(int page = 1, int pageSize = 10, string lang = "en", Guid? categoryId = null, string? search = null)
    {
        var url = $"api/courses?page={page}&pageSize={pageSize}&lang={lang}";
        if (categoryId is { } catId)
            url += $"&categoryId={catId}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";

        return await GetAsync<PagedResult<CourseListItemDto>>(url)
           ?? new PagedResult<CourseListItemDto>([], 0, page, pageSize);
    }

    public Task<CourseDetailDto?> GetCourseAsync(Guid courseId, string lang = "en")
        => GetAsync<CourseDetailDto>($"api/courses/{courseId}?lang={lang}");

    public async Task<EnrollmentResultDto?> EnrollAsync(Guid courseId)
    {
        await EnsureAuthorizationAsync();
        var response = await Http.PostAsync($"api/courses/{courseId}/enroll", null);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return await response.Content.ReadFromJsonAsync<EnrollmentResultDto>();
        return null;
    }

    public Task<LessonDetailDto?> GetLessonAsync(Guid lessonId, string lang = "en")
        => GetAsync<LessonDetailDto>($"api/courses/lessons/{lessonId}?lang={lang}");

    public Task<LessonCompletionResultDto?> CompleteLessonAsync(Guid lessonId)
        => PostAsync<LessonCompletionResultDto>($"api/courses/lessons/{lessonId}/complete");
}
