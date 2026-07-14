using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Application.DTOs;
using System.Security.Claims;
using System.Text.Json;


namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin endpoints for managing assessment definitions.
/// </summary>
[ApiController]
[Route("api/admin/assessments")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Assessments")]
[Produces("application/json", "application/problem+json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminAssessmentsController : ControllerBase
{
    // EF Core DB context used to read and write application data
    private readonly IApplicationDbContext _db;
    private readonly ILogger<AdminAssessmentsController> _logger;
    private readonly IHtmlSanitizer _sanitizer;

    public AdminAssessmentsController(IApplicationDbContext db, ILogger<AdminAssessmentsController> logger, IHtmlSanitizer sanitizer)
    {
        _db = db;
        _logger = logger;
        _sanitizer = sanitizer;
    }

    // Helper property to get the current authenticated user's ID or throw if missing
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found");

    /// <summary>
    /// Get a paged list of assessment definitions (published and unpublished).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AssessmentDefinitionListItemDto>>> GetAssessments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "createdat",
        [FromQuery] string sortDir = "desc")
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        var query = _db.AssessmentDefinitions.AsNoTracking();

        var totalCount = await query.CountAsync();

        var items = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AssessmentDefinitionListItemDto(
                a.Id,
                a.Key,
                a.TitleEn,
                a.IsPublished,
                a.Submissions.Count,
                a.CreatedAt,
                a.Category != null ? a.Category.NameEn : null,
                a.RequiredTier
            ))
            .ToListAsync();

        return Ok(new PagedResult<AssessmentDefinitionListItemDto>(items, totalCount, page, pageSize, sortBy, sortDir));
    }

    /// <summary>
    /// Get a single assessment definition by id.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminAssessmentDefinitionDto>> GetAssessmentById(Guid id)
    {
        var assessment = await _db.AssessmentDefinitions
            .AsNoTracking()
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (assessment == null)
        {
            return NotFound();
        }

        return Ok(assessment.ToAdminDto(assessment.Category?.NameEn));
    }

    /// <summary>
    /// Create a new assessment definition.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<AdminAssessmentDefinitionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminAssessmentDefinitionDto>> CreateAssessment([FromBody] SaveAssessmentDefinitionRequest request)
    {
        // Ensure the requested key is unique to avoid duplicates
        if (await _db.AssessmentDefinitions.AnyAsync(a => a.Key == request.Key))
        {
            // API-3: 409, not 400 — this is "retry with a different key", not "fix your input shape".
            return Conflict($"Assessment with key '{request.Key}' already exists");
        }

        // DQ-4: reject malformed/structurally-wrong schemas here instead of only failing later
        // at render time (AssessmentService.ResolveSchemaJsonByLang) or submit time.
        if (!IsStructurallyValidSchemaJson(request.SchemaJson))
        {
            return BadRequest("SchemaJson must be a JSON object with a 'questions' or 'sections' array.");
        }

        var categoryId = await AdminCategoryService.ResolveCategoryAsync(_db, request.CategoryId, request.NewCategoryName);

        // Build a new assessment entity with provided data and initial metadata
        var assessment = new AssessmentDefinition
        {
            Id = Guid.NewGuid(),
            Key = request.Key,
            TitleEn = request.TitleEn,
            TitleEl = request.TitleEl,
            DescriptionEn = request.DescriptionEn is not null ? _sanitizer.Sanitize(request.DescriptionEn) : null,
            DescriptionEl = request.DescriptionEl is not null ? _sanitizer.Sanitize(request.DescriptionEl) : null,
            SchemaJson = request.SchemaJson,
            CategoryId = categoryId,
            IsPublished = false,
            UpdatedByUserId = UserId
        };

        // Add and persist the new entity
        _db.AssessmentDefinitions.Add(assessment);
        await _db.SaveChangesAsync();

        var categoryNameEn = await GetCategoryNameEnAsync(categoryId);

        // API-4: point Location at the by-id GET, not the paged list (which would resolve to
        // "/api/admin/assessments?id={guid}" — a bogus query string on a collection route).
        return CreatedAtAction(nameof(GetAssessmentById), new
        {
            id = assessment.Id
        }, assessment.ToAdminDto(categoryNameEn));
    }

    /// <summary>
    /// Update an existing assessment definition.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminAssessmentDefinitionDto>> UpdateAssessment(Guid id, [FromBody] SaveAssessmentDefinitionRequest request)
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
            // API-3: 409, not 400 — this is "retry with a different key", not "fix your input shape".
            return Conflict($"Assessment with key '{request.Key}' already exists");
        }

        // DQ-4: reject malformed/structurally-wrong schemas here instead of only failing later
        // at render time (AssessmentService.ResolveSchemaJsonByLang) or submit time.
        if (!IsStructurallyValidSchemaJson(request.SchemaJson))
        {
            return BadRequest("SchemaJson must be a JSON object with a 'questions' or 'sections' array.");
        }

        var categoryId = await AdminCategoryService.ResolveCategoryAsync(_db, request.CategoryId, request.NewCategoryName);

        // Apply updates and metadata (updated time and user)
        assessment.Key = request.Key;
        assessment.TitleEn = request.TitleEn;
        assessment.TitleEl = request.TitleEl;
        assessment.DescriptionEn = request.DescriptionEn is not null ? _sanitizer.Sanitize(request.DescriptionEn) : null;
        assessment.DescriptionEl = request.DescriptionEl is not null ? _sanitizer.Sanitize(request.DescriptionEl) : null;
        assessment.SchemaJson = request.SchemaJson;
        assessment.CategoryId = categoryId;
        assessment.UpdatedAt = DateTimeOffset.UtcNow;
        assessment.UpdatedByUserId = UserId;

        // Persist changes
        await _db.SaveChangesAsync();

        var categoryNameEn = await GetCategoryNameEnAsync(categoryId);

        // Map updated entity to DTO and return 200 OK
        return Ok(assessment.ToAdminDto(categoryNameEn));
    }

    private static bool IsStructurallyValidSchemaJson(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                ((root.TryGetProperty("questions", out var questions) && questions.ValueKind == JsonValueKind.Array) ||
                 (root.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<string?> GetCategoryNameEnAsync(Guid? categoryId)
    {
        if (categoryId is not { } id)
            return null;

        return await _db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => c.NameEn)
            .FirstOrDefaultAsync();
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
    /// Unpublish an assessment (hide it from students).
    /// </summary>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> UnpublishAssessment(Guid id)
    {
        var assessment = await _db.AssessmentDefinitions.FindAsync(id);
        if (assessment == null)
        {
            return NotFound();
        }

        if (assessment.IsPublished)
        {
            assessment.IsPublished = false;
            assessment.PublishedAt = null;
            assessment.UpdatedAt = DateTimeOffset.UtcNow;
            assessment.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>
    /// Delete an assessment definition and all its submissions.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAssessment(Guid id)
    {
        var assessment = await _db.AssessmentDefinitions
            .Include(a => a.Submissions)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment == null)
        {
            return NotFound();
        }

        _db.AssessmentSubmissions.RemoveRange(assessment.Submissions);
        _db.AssessmentDefinitions.Remove(assessment);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Get a paged list of submissions for a specific assessment.
    /// </summary>
    [HttpGet("{id:guid}/submissions")]
    public async Task<ActionResult<PagedResult<AssessmentSubmissionListItemDto>>> GetSubmissions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "submittedat",
        [FromQuery] string sortDir = "desc")
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        // Ensure the assessment exists before querying submissions
        var assessmentExists = await _db.AssessmentDefinitions.AnyAsync(a => a.Id == id);
        if (!assessmentExists)
        {
            return NotFound();
        }

        var query = _db.AssessmentSubmissions
            .AsNoTracking()
            .Where(s => s.AssessmentDefinitionId == id);

        var totalCount = await query.CountAsync();

        // Order, paginate, then project to DTO — EF Core resolves the User navigation via SELECT JOIN
        var items = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new AssessmentSubmissionListItemDto(
                s.Id,
                s.UserId,
                s.User.Email ?? "N/A",
                s.User.DisplayName ?? $"{s.User.FirstName} {s.User.LastName}",
                s.AnswersJson,
                s.SummaryJson,
                s.SubmittedAt
            ))
            .ToListAsync();

        return Ok(new PagedResult<AssessmentSubmissionListItemDto>(items, totalCount, page, pageSize, sortBy, sortDir));
    }
}
