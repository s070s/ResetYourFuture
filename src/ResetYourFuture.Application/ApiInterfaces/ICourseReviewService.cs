using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Course reviews: one per enrolled student per course, admin-moderated (Pending → Approved/
/// Rejected) before it's visible to other students. Mirrors the Testimonial moderation flow.
/// </summary>
public interface ICourseReviewService
{
    /// <summary>Approved reviews for a course, newest first — what other students see.</summary>
    Task<IReadOnlyList<CourseReviewDto>> GetApprovedForCourseAsync(Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>Average rating + count of approved reviews for one course, or null if none yet.</summary>
    Task<CourseRatingSummaryDto?> GetRatingSummaryAsync(Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>Batched version of <see cref="GetRatingSummaryAsync"/> for course list/catalog pages.</summary>
    Task<Dictionary<Guid, CourseRatingSummaryDto>> GetRatingSummariesAsync(
        IEnumerable<Guid> courseIds, CancellationToken cancellationToken = default);

    /// <summary>The current user's own review on a course, at any moderation status, or null.</summary>
    Task<MyCourseReviewDto?> GetMyReviewAsync(string userId, Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the user's review (one per course). Requires an active
    /// enrollment; editing resets the review to Pending for re-moderation.</summary>
    Task<ServiceResult<MyCourseReviewDto>> SaveMyReviewAsync(
        string userId, Guid courseId, SaveCourseReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Admin moderation queue, paged and sortable, optionally filtered by status.</summary>
    Task<PagedResult<AdminCourseReviewDto>> GetPagedAsync(
        int page, int pageSize, string sortBy, string sortDir, string? statusFilter, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> RejectAsync(Guid id, CancellationToken cancellationToken = default);
}
