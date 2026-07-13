using System.Linq.Expressions;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Application.Mappings;

/// <summary>
/// Shared course-review mappers (MAINT-1). MyCourseReviewDto was hand-built twice in
/// CourseReviewService (in-query read + post-save). Keep the pair's field order in sync.
/// </summary>
public static class ReviewMappings
{
    /// <summary>For IQueryable.Select.</summary>
    public static readonly Expression<Func<CourseReview, MyCourseReviewDto>> MyReviewProjection =
        r => new MyCourseReviewDto(r.Id, r.Rating, r.Body, r.Status.ToString(), r.CreatedAt, r.UpdatedAt);

    public static MyCourseReviewDto ToMyReviewDto(this CourseReview r) =>
        new(r.Id, r.Rating, r.Body, r.Status.ToString(), r.CreatedAt, r.UpdatedAt);
}
