using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for the admin course-review moderation API.
/// </summary>
public interface IAdminCourseReviewConsumer
{
    Task<PagedResult<AdminCourseReviewDto>?> GetAllAsync(
        int page = 1, int pageSize = 10, string sortBy = "createdat", string sortDir = "desc", string? status = null);
    Task<bool> ApproveAsync(Guid id);
    Task<bool> RejectAsync(Guid id);
}
