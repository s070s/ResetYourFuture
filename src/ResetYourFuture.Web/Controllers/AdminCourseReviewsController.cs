using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Extensions;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin moderation queue for course reviews: list (all statuses, sortable), approve, reject.
/// </summary>
[ApiController]
[Route("api/admin/course-reviews")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Course Reviews")]
[Produces("application/json", "application/problem+json")]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminCourseReviewsController(ICourseReviewService reviews) : ControllerBase
{
    /// <summary>Get a paged, sortable list of reviews, optionally filtered by status
    /// (Pending/Approved/Rejected).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCourseReviewDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "createdat",
        [FromQuery] string sortDir = "desc",
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        var result = await reviews.GetPagedAsync(page, pageSize, sortBy, sortDir, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>Approve a pending review, making it visible on the course page.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<string>> Approve(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await reviews.ApproveAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Reject a pending review.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<string>> Reject(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await reviews.RejectAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }
}
