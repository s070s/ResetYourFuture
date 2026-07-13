using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin course-review moderation API.
/// </summary>
public class AdminCourseReviewConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider)
    : ApiClientBase(http, tokenProvider), IAdminCourseReviewConsumer
{
    public Task<PagedResult<AdminCourseReviewDto>?> GetAllAsync(
        int page = 1, int pageSize = 10, string sortBy = "createdat", string sortDir = "desc", string? status = null)
    {
        var url = $"api/admin/course-reviews?page={page}&pageSize={pageSize}&sortBy={sortBy}&sortDir={sortDir}";
        if (!string.IsNullOrWhiteSpace(status))
            url += $"&status={status}";
        return GetAsync<PagedResult<AdminCourseReviewDto>>(url);
    }

    public Task<bool> ApproveAsync(Guid id)
        => ActionAsync($"api/admin/course-reviews/{id}/approve");

    public Task<bool> RejectAsync(Guid id)
        => ActionAsync($"api/admin/course-reviews/{id}/reject");
}
