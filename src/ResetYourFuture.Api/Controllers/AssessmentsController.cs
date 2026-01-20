using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Shared.Models.Assessments;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Student-facing assessment endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssessmentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AssessmentsController> _logger;

    public AssessmentsController(ApplicationDbContext db, ILogger<AssessmentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get all published assessments.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AssessmentDefinitionDto>>> GetPublishedAssessments()
    {
        var assessments = await _db.AssessmentDefinitions
            .Where(a => a.IsPublished)
            .OrderBy(a => a.Title)
            .Select(a => new AssessmentDefinitionDto(
                a.Id,
                a.Key,
                a.Title,
                a.Description,
                a.SchemaJson,
                a.IsPublished,
                a.CreatedAt,
                a.UpdatedAt,
                a.PublishedAt
            ))
            .ToListAsync();

        return Ok(assessments);
    }

    /// <summary>
    /// Get a specific published assessment by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssessmentDefinitionDto>> GetAssessment(Guid id)
    {
        var assessment = await _db.AssessmentDefinitions
            .Where(a => a.Id == id && a.IsPublished)
            .Select(a => new AssessmentDefinitionDto(
                a.Id,
                a.Key,
                a.Title,
                a.Description,
                a.SchemaJson,
                a.IsPublished,
                a.CreatedAt,
                a.UpdatedAt,
                a.PublishedAt
            ))
            .FirstOrDefaultAsync();

        if (assessment == null)
        {
            return NotFound();
        }

        return Ok(assessment);
    }

    /// <summary>
    /// Submit answers for an assessment.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<AssessmentSubmissionDto>> SubmitAssessment(Guid id, [FromBody] SubmitAssessmentRequest request)
    {
        var assessment = await _db.AssessmentDefinitions
            .Where(a => a.Id == id && a.IsPublished)
            .FirstOrDefaultAsync();

        if (assessment == null)
        {
            return NotFound("Assessment not found or not published");
        }

        var submission = new Domain.Entities.AssessmentSubmission
        {
            Id = Guid.NewGuid(),
            AssessmentDefinitionId = id,
            UserId = UserId,
            AnswersJson = request.AnswersJson,
            SummaryJson = request.SummaryJson,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        _db.AssessmentSubmissions.Add(submission);
        await _db.SaveChangesAsync();

        var dto = new AssessmentSubmissionDto(
            submission.Id,
            submission.AssessmentDefinitionId,
            assessment.Title,
            submission.AnswersJson,
            submission.SummaryJson,
            submission.SubmittedAt
        );

        return Ok(dto);
    }

    /// <summary>
    /// Get current user's assessment submissions (history).
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<AssessmentSubmissionDto>>> GetMySubmissions()
    {
        var submissions = await _db.AssessmentSubmissions
            .Where(s => s.UserId == UserId)
            .Include(s => s.AssessmentDefinition)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new AssessmentSubmissionDto(
                s.Id,
                s.AssessmentDefinitionId,
                s.AssessmentDefinition.Title,
                s.AnswersJson,
                s.SummaryJson,
                s.SubmittedAt
            ))
            .ToListAsync();

        return Ok(submissions);
    }
}
