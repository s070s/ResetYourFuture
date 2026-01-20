using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Shared.Models.Assessments;
using System.Security.Claims;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin endpoints for managing assessment definitions.
/// </summary>
[ApiController]
[Route("api/admin/assessments")]
[Authorize(Policy = "AdminOnly")]
public class AdminAssessmentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AdminAssessmentsController> _logger;

    public AdminAssessmentsController(ApplicationDbContext db, ILogger<AdminAssessmentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get all assessment definitions (published and unpublished).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AssessmentDefinitionListItemDto>>> GetAssessments()
    {
        var assessments = await _db.AssessmentDefinitions
            .Select(a => new AssessmentDefinitionListItemDto(
                a.Id,
                a.Key,
                a.Title,
                a.IsPublished,
                a.Submissions.Count,
                a.CreatedAt
            ))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(assessments);
    }

    /// <summary>
    /// Create a new assessment definition.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AssessmentDefinitionDto>> CreateAssessment([FromBody] SaveAssessmentDefinitionRequest request)
    {
        // Check for duplicate key
        if (await _db.AssessmentDefinitions.AnyAsync(a => a.Key == request.Key))
        {
            return BadRequest($"Assessment with key '{request.Key}' already exists");
        }

        var assessment = new AssessmentDefinition
        {
            Id = Guid.NewGuid(),
            Key = request.Key,
            Title = request.Title,
            Description = request.Description,
            SchemaJson = request.SchemaJson,
            IsPublished = false,
            UpdatedByUserId = UserId
        };

        _db.AssessmentDefinitions.Add(assessment);
        await _db.SaveChangesAsync();

        var dto = new AssessmentDefinitionDto(
            assessment.Id,
            assessment.Key,
            assessment.Title,
            assessment.Description,
            assessment.SchemaJson,
            assessment.IsPublished,
            assessment.CreatedAt,
            assessment.UpdatedAt,
            assessment.PublishedAt
        );

        return CreatedAtAction(nameof(GetAssessments), new { id = assessment.Id }, dto);
    }

    /// <summary>
    /// Update an existing assessment definition.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssessmentDefinitionDto>> UpdateAssessment(Guid id, [FromBody] SaveAssessmentDefinitionRequest request)
    {
        var assessment = await _db.AssessmentDefinitions.FindAsync(id);
        if (assessment == null)
        {
            return NotFound();
        }

        // Check for duplicate key (excluding current assessment)
        if (await _db.AssessmentDefinitions.AnyAsync(a => a.Key == request.Key && a.Id != id))
        {
            return BadRequest($"Assessment with key '{request.Key}' already exists");
        }

        assessment.Key = request.Key;
        assessment.Title = request.Title;
        assessment.Description = request.Description;
        assessment.SchemaJson = request.SchemaJson;
        assessment.UpdatedAt = DateTimeOffset.UtcNow;
        assessment.UpdatedByUserId = UserId;

        await _db.SaveChangesAsync();

        var dto = new AssessmentDefinitionDto(
            assessment.Id,
            assessment.Key,
            assessment.Title,
            assessment.Description,
            assessment.SchemaJson,
            assessment.IsPublished,
            assessment.CreatedAt,
            assessment.UpdatedAt,
            assessment.PublishedAt
        );

        return Ok(dto);
    }

    /// <summary>
    /// Publish an assessment (make it available to students).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishAssessment(Guid id)
    {
        var assessment = await _db.AssessmentDefinitions.FindAsync(id);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!assessment.IsPublished)
        {
            assessment.IsPublished = true;
            assessment.PublishedAt = DateTimeOffset.UtcNow;
            assessment.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>
    /// Get all submissions for a specific assessment.
    /// </summary>
    [HttpGet("{id:guid}/submissions")]
    public async Task<ActionResult<List<AssessmentSubmissionListItemDto>>> GetSubmissions(Guid id)
    {
        var assessment = await _db.AssessmentDefinitions.FindAsync(id);
        if (assessment == null)
        {
            return NotFound();
        }

        var submissions = await _db.AssessmentSubmissions
            .Where(s => s.AssessmentDefinitionId == id)
            .Include(s => s.User)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new AssessmentSubmissionListItemDto(
                s.Id,
                s.UserId,
                s.User.Email ?? "N/A",
                s.User.DisplayName ?? $"{s.User.FirstName} {s.User.LastName}",
                s.SubmittedAt
            ))
            .ToListAsync();

        return Ok(submissions);
    }
}
