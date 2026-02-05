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
    // EF Core DB context used to read and write application data
    private readonly ApplicationDbContext _db;
    // Logger for recording informational and error messages
    private readonly ILogger<AdminAssessmentsController> _logger;

    // Constructor receives dependencies via dependency injection
    public AdminAssessmentsController(ApplicationDbContext db, ILogger<AdminAssessmentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Helper property to get the current authenticated user's ID or throw if missing
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get all assessment definitions (published and unpublished).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AssessmentDefinitionListItemDto>>> GetAssessments()
    {
        // Query DB for assessment definitions and project to lightweight DTOs with submission counts
        var assessments = await _db.AssessmentDefinitions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AssessmentDefinitionListItemDto(
                a.Id,
                a.Key,
                a.Title,
                a.IsPublished,
                a.Submissions.Count,
                a.CreatedAt
            ))
            .ToListAsync();

        // Return 200 OK with the list of assessments
        return Ok(assessments);
    }

    /// <summary>
    /// Create a new assessment definition.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AssessmentDefinitionDto>> CreateAssessment([FromBody] SaveAssessmentDefinitionRequest request)
    {
        // Ensure the requested key is unique to avoid duplicates
        if (await _db.AssessmentDefinitions.AnyAsync(a => a.Key == request.Key))
        {
            return BadRequest($"Assessment with key '{request.Key}' already exists");
        }

        // Build a new assessment entity with provided data and initial metadata
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

        // Add and persist the new entity
        _db.AssessmentDefinitions.Add(assessment);
        await _db.SaveChangesAsync();

        // Map persisted entity to DTO for response
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

        // Return 201 Created with location header pointing to the assessments list endpoint
        return CreatedAtAction(nameof(GetAssessments), new { id = assessment.Id }, dto);
    }

    /// <summary>
    /// Update an existing assessment definition.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssessmentDefinitionDto>> UpdateAssessment(Guid id, [FromBody] SaveAssessmentDefinitionRequest request)
    {
        // Try to find the assessment by id and return 404 if not found
        var assessment = await _db.AssessmentDefinitions.FindAsync(id);
        if (assessment == null)
        {
            return NotFound();
        }

        // Ensure the new key does not clash with another assessment
        if (await _db.AssessmentDefinitions.AnyAsync(a => a.Key == request.Key && a.Id != id))
        {
            return BadRequest($"Assessment with key '{request.Key}' already exists");
        }

        // Apply updates and metadata (updated time and user)
        assessment.Key = request.Key;
        assessment.Title = request.Title;
        assessment.Description = request.Description;
        assessment.SchemaJson = request.SchemaJson;
        assessment.UpdatedAt = DateTimeOffset.UtcNow;
        assessment.UpdatedByUserId = UserId;

        // Persist changes
        await _db.SaveChangesAsync();

        // Map updated entity to DTO and return 200 OK
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
        // Find the assessment or return 404
        var assessment = await _db.AssessmentDefinitions.FindAsync(id);
        if (assessment == null)
        {
            return NotFound();
        }

        // If not already published, mark published, set timestamps and user, then save
        if (!assessment.IsPublished)
        {
            assessment.IsPublished = true;
            assessment.PublishedAt = DateTimeOffset.UtcNow;
            assessment.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        // Return 204 No Content to indicate success without body
        return NoContent();
    }

    /// <summary>
    /// Get all submissions for a specific assessment.
    /// </summary>
    [HttpGet("{id:guid}/submissions")]
    public async Task<ActionResult<List<AssessmentSubmissionListItemDto>>> GetSubmissions(Guid id)
    {
        // Ensure the assessment exists before querying submissions
        var assessment = await _db.AssessmentDefinitions.FindAsync(id);
        if (assessment == null)
        {
            return NotFound();
        }

        // Query submissions, include the user navigation, map to DTOs and order by submitted time
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

        // Return 200 OK with the list of submissions
        return Ok(submissions);
    }
}
