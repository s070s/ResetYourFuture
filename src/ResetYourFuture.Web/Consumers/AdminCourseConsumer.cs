using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin course management API.
/// </summary>
public class AdminCourseConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider) : ApiClientBase(http, tokenProvider), IAdminCourseConsumer
{
    public Task<PagedResult<AdminCourseDto>?> GetCoursesAsync(int page = 1, int pageSize = 10, string sortBy = "createdat", string sortDir = "desc")
        => GetAsync<PagedResult<AdminCourseDto>>($"api/admin/courses?page={page}&pageSize={pageSize}&sortBy={sortBy}&sortDir={sortDir}");

    public Task<AdminCourseDto?> GetCourseAsync(Guid id)
        => GetAsync<AdminCourseDto>($"api/admin/courses/{id}");

    public Task<AdminCourseDto?> CreateCourseAsync(SaveCourseRequest request)
        => PostJsonAsync<SaveCourseRequest, AdminCourseDto>("api/admin/courses", request);

    public Task<AdminCourseDto?> UpdateCourseAsync(Guid id, SaveCourseRequest request)
        => PutJsonAsync<SaveCourseRequest, AdminCourseDto>($"api/admin/courses/{id}", request);

    public Task<bool> DeleteCourseAsync(Guid id)
        => DeleteAsync($"api/admin/courses/{id}");

    public Task<bool> PublishCourseAsync(Guid id)
        => ActionAsync($"api/admin/courses/{id}/publish");

    public Task<bool> UnpublishCourseAsync(Guid id)
        => ActionAsync($"api/admin/courses/{id}/unpublish");
}
