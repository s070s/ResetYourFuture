using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Extensions;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Student-facing assessment endpoints.
/// </summary>
[ApiController]
[Route("api/assessments")]
[Authorize]
[Tags("Assessments")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AssessmentsController(IAssessmentService assessmentService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get a paged list of published assessments.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AssessmentDefinitionDto>>> GetPublishedAssessments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string lang = "en",
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await assessmentService.GetPublishedAssessmentsAsync(UserId, page, pageSize, lang, categoryId, search, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Get a specific published assessment by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssessmentDefinitionDto>> GetAssessment(Guid id, [FromQuery] string lang = "en", CancellationToken cancellationToken = default)
    {
        var result = await assessmentService.GetAssessmentAsync(UserId, id, lang, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Submit answers for an assessment.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<AssessmentSubmissionDto>> SubmitAssessment(Guid id, [FromBody] SubmitAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        var result = await assessmentService.SubmitAssessmentAsync(UserId, id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Get current user's assessment submissions (history).
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<AssessmentSubmissionDto>>> GetMySubmissions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "submittedat",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var submissions = await assessmentService.GetMySubmissionsAsync(UserId, page, pageSize, sortBy, sortDir, cancellationToken);
        return Ok(submissions);
    }
}
