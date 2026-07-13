using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Extensions;

namespace ResetYourFuture.Application.ApiServices;

/// <inheritdoc cref="ICourseReviewService"/>
public class CourseReviewService(IApplicationDbContext db, INotificationDispatcher notifications, ILogger<CourseReviewService> logger)
    : ICourseReviewService
{
    public async Task<IReadOnlyList<CourseReviewDto>> GetApprovedForCourseAsync(
        Guid courseId, CancellationToken cancellationToken = default)
    {
        // Author name computed inline (not via a helper method) so EF can translate the whole
        // projection to SQL instead of falling back to client evaluation.
        return await db.CourseReviews
            .AsNoTracking()
            .Where(r => r.CourseId == courseId && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new CourseReviewDto(
                r.Id,
                !string.IsNullOrWhiteSpace(r.User!.DisplayName) ? r.User.DisplayName! : (r.User.FirstName + " " + r.User.LastName).Trim(),
                r.Rating, r.Body, r.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CourseRatingSummaryDto?> GetRatingSummaryAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var summaries = await GetRatingSummariesAsync([courseId], cancellationToken);
        return summaries.GetValueOrDefault(courseId);
    }

    public async Task<Dictionary<Guid, CourseRatingSummaryDto>> GetRatingSummariesAsync(
        IEnumerable<Guid> courseIds, CancellationToken cancellationToken = default)
    {
        var ids = courseIds.ToList();
        if (ids.Count == 0)
            return [];

        var rows = await db.CourseReviews
            .AsNoTracking()
            .Where(r => ids.Contains(r.CourseId) && r.Status == ReviewStatus.Approved)
            .GroupBy(r => r.CourseId)
            .Select(g => new { CourseId = g.Key, Average = g.Average(r => r.Rating), Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.CourseId, r => new CourseRatingSummaryDto(Math.Round(r.Average, 1), r.Count));
    }

    public async Task<MyCourseReviewDto?> GetMyReviewAsync(string userId, Guid courseId, CancellationToken cancellationToken = default)
    {
        return await db.CourseReviews
            .AsNoTracking()
            .Where(r => r.CourseId == courseId && r.UserId == userId)
            .Select(ReviewMappings.MyReviewProjection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<MyCourseReviewDto>> SaveMyReviewAsync(
        string userId, Guid courseId, SaveCourseReviewRequest request, CancellationToken cancellationToken = default)
    {
        var isEnrolled = await db.Enrollments.AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.CourseId == courseId, cancellationToken);
        if (!isEnrolled)
            return ServiceResult<MyCourseReviewDto>.Forbidden(error: "Only enrolled students can review this course.");

        var review = await db.CourseReviews.FirstOrDefaultAsync(r => r.CourseId == courseId && r.UserId == userId, cancellationToken);

        if (review is null)
        {
            review = new CourseReview
            {
                CourseId = courseId,
                UserId = userId,
                Rating = request.Rating,
                Body = request.Body,
                Status = ReviewStatus.Pending
            };
            db.CourseReviews.Add(review);
        }
        else
        {
            // Editing changes the content, so it goes back through moderation.
            review.Rating = request.Rating;
            review.Body = request.Body;
            review.Status = ReviewStatus.Pending;
            review.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<MyCourseReviewDto>.Ok(review.ToMyReviewDto());
    }

    public async Task<PagedResult<AdminCourseReviewDto>> GetPagedAsync(
        int page, int pageSize, string sortBy, string sortDir, string? statusFilter, CancellationToken cancellationToken = default)
    {
        var query = db.CourseReviews.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<ReviewStatus>(statusFilter, ignoreCase: true, out var status))
            query = query.Where(r => r.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminCourseReviewDto(
                r.Id, r.CourseId, r.Course!.TitleEn,
                !string.IsNullOrWhiteSpace(r.User!.DisplayName) ? r.User.DisplayName! : (r.User.FirstName + " " + r.User.LastName).Trim(),
                r.Rating, r.Body, r.Status.ToString(), r.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminCourseReviewDto>(items, totalCount, page, pageSize, sortBy, sortDir);
    }

    public async Task<ServiceResult<string>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var review = await db.CourseReviews.Include(r => r.Course).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (review is null)
            return ServiceResult<string>.NotFound(error: "Review not found.");

        review.Status = ReviewStatus.Approved;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await notifications.DispatchAsync(
                review.UserId, NotificationType.ReviewApproved, "ReviewApproved",
                [review.Course?.TitleEn ?? string.Empty], $"courses/{review.CourseId}", cancellationToken);
        }
        catch (Exception ex)
        {
            // Notification delivery is best-effort — the moderation action itself already succeeded.
            logger.LogWarning(ex, "Failed to dispatch review-approved notification for review {ReviewId}.", id);
        }

        return ServiceResult<string>.Ok("Review approved.");
    }

    public async Task<ServiceResult<string>> RejectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var review = await db.CourseReviews.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (review is null)
            return ServiceResult<string>.NotFound(error: "Review not found.");

        review.Status = ReviewStatus.Rejected;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<string>.Ok("Review rejected.");
    }
}
