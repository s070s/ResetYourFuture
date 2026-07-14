using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Web.Extensions;
using ResetYourFuture.Application.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// API endpoints for course discovery, enrollment, and consumption.
/// All endpoints require authentication.
/// </summary>
[ApiController]
[Route("api/courses")]
[Authorize]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class CoursesController(ICourseService courseService, ICourseReviewService reviewService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found in claims");

    /// <summary>
    /// Get a paged list of all published courses with enrollment status.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseListItemDto>>> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string lang = "en",
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        var result = await courseService.GetPublishedCoursesAsync(UserId, page, pageSize, lang, categoryId, search, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get full course detail including modules, lessons, and progress.
    /// </summary>
    [HttpGet("{courseId:guid}")]
    public async Task<ActionResult<CourseDetailDto>> GetCourse(Guid courseId, [FromQuery] string lang = "en", CancellationToken cancellationToken = default)
    {
        var dto = await courseService.GetCourseDetailAsync(UserId, courseId, lang, cancellationToken);
        return dto is not null
            ? Ok(dto)
            : Problem(detail: "Course not found", statusCode: StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Enroll the current user in a course.
    /// Admins cannot enroll - this is for students only.
    /// </summary>
    [HttpPost("{courseId:guid}/enroll")]
    public async Task<ActionResult<EnrollmentResultDto>> Enroll(Guid courseId, CancellationToken cancellationToken = default)
    {
        if (User.IsInRole("Admin"))
            return StatusCode(403, new EnrollmentResultDto(false, "Administrators cannot enroll in courses", null));

        var result = await courseService.EnrollAsync(UserId, courseId, cancellationToken);
        return result.ToEmbeddedActionResult();
    }

    /// <summary>
    /// Get full lesson detail for the lesson viewer.
    /// </summary>
    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<ActionResult<LessonDetailDto>> GetLesson(Guid lessonId, [FromQuery] string lang = "en", CancellationToken cancellationToken = default)
    {
        var result = await courseService.GetLessonDetailAsync(UserId, lessonId, lang, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Mark a lesson as completed.
    /// </summary>
    [HttpPost("lessons/{lessonId:guid}/complete")]
    public async Task<ActionResult<LessonCompletionResultDto>> CompleteLesson(Guid lessonId, CancellationToken cancellationToken = default)
    {
        var result = await courseService.CompleteLessonAsync(UserId, lessonId, cancellationToken);
        return result.ToEmbeddedActionResult();
    }

    /// <summary>
    /// Get approved reviews, the aggregate rating, and (for the signed-in user) their own
    /// review at any moderation status — everything the course page needs in one call.
    /// </summary>
    [HttpGet("{courseId:guid}/reviews")]
    public async Task<ActionResult<CourseReviewsResponseDto>> GetReviews(Guid courseId, CancellationToken cancellationToken = default)
    {
        var reviews = await reviewService.GetApprovedForCourseAsync(courseId, cancellationToken);
        var summary = await reviewService.GetRatingSummaryAsync(courseId, cancellationToken);
        var mine = User.IsInRole("Admin") ? null : await reviewService.GetMyReviewAsync(UserId, courseId, cancellationToken);

        return Ok(new CourseReviewsResponseDto([.. reviews], summary, mine));
    }

    /// <summary>
    /// Create or update the current user's review for this course (one per course). Requires
    /// an active enrollment; editing resets the review to Pending for re-moderation.
    /// </summary>
    [HttpPost("{courseId:guid}/reviews")]
    public async Task<ActionResult<MyCourseReviewDto>> SaveReview(
        Guid courseId, [FromBody] SaveCourseReviewRequest request, CancellationToken cancellationToken = default)
    {
        var result = await reviewService.SaveMyReviewAsync(UserId, courseId, request, cancellationToken);
        return result.ToActionResult(this);
    }
}
