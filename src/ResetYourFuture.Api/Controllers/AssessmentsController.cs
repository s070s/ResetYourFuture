using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Student-facing assessment endpoints.
/// </summary>
[ApiController]
[Route( "api/[controller]" )]
[Authorize]
public class AssessmentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<AssessmentsController> _logger;

    public AssessmentsController(
        ApplicationDbContext db ,
        ISubscriptionService subscriptionService ,
        ILogger<AssessmentsController> logger )
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue( ClaimTypes.NameIdentifier )
        ?? throw new UnauthorizedAccessException( "User ID not found" );

    /// <summary>
    /// Get a paged list of published assessments.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AssessmentDefinitionDto>>> GetPublishedAssessments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10 )
    {
        if ( page < 1 ) page = 1;
        if ( pageSize < 1 || pageSize > 100 ) pageSize = 10;

        var query = _db.AssessmentDefinitions
            .Where( a => a.IsPublished )
            .OrderBy( a => a.Title );

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .Select( a => new AssessmentDefinitionDto(
                a.Id ,
                a.Key ,
                a.Title ,
                a.Description ,
                a.SchemaJson ,
                a.IsPublished ,
                a.CreatedAt ,
                a.UpdatedAt ,
                a.PublishedAt
            ) )
            .ToListAsync();

        return Ok( new PagedResult<AssessmentDefinitionDto>( items , totalCount , page , pageSize ) );
    }

    /// <summary>
    /// Get a specific published assessment by ID.
    /// </summary>
    [HttpGet( "{id:guid}" )]
    public async Task<ActionResult<AssessmentDefinitionDto>> GetAssessment( Guid id )
    {
        var assessment = await _db.AssessmentDefinitions
            .Where( a => a.Id == id && a.IsPublished )
            .Select( a => new AssessmentDefinitionDto(
                a.Id ,
                a.Key ,
                a.Title ,
                a.Description ,
                a.SchemaJson ,
                a.IsPublished ,
                a.CreatedAt ,
                a.UpdatedAt ,
                a.PublishedAt
            ) )
            .FirstOrDefaultAsync();

        if ( assessment == null )
        {
            return NotFound();
        }

        return Ok( assessment );
    }

    /// <summary>
    /// Submit answers for an assessment.
    /// </summary>
    [HttpPost( "{id:guid}/submit" )]
    public async Task<ActionResult<AssessmentSubmissionDto>> SubmitAssessment( Guid id , [FromBody] SubmitAssessmentRequest request )
    {
        var assessment = await _db.AssessmentDefinitions
            .Where( a => a.Id == id && a.IsPublished )
            .FirstOrDefaultAsync();

        if ( assessment == null )
        {
            return NotFound( "Assessment not found or not published" );
        }

        // Check subscription tier
        var userTier = await _subscriptionService.GetUserTierAsync( UserId );
        if ( userTier < assessment.RequiredTier )
        {
            return StatusCode( 403 , $"This assessment requires a {assessment.RequiredTier} subscription or higher." );
        }

        var submission = new Domain.Entities.AssessmentSubmission
        {
            Id = Guid.NewGuid() ,
            AssessmentDefinitionId = id ,
            UserId = UserId ,
            AnswersJson = request.AnswersJson ,
            SummaryJson = request.SummaryJson ,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        _db.AssessmentSubmissions.Add( submission );
        await _db.SaveChangesAsync();

        var dto = new AssessmentSubmissionDto(
            submission.Id ,
            submission.AssessmentDefinitionId ,
            assessment.Title ,
            submission.AnswersJson ,
            submission.SummaryJson ,
            submission.SubmittedAt
        );

        return Ok( dto );
    }

    /// <summary>
    /// Get current user's assessment submissions (history).
    /// </summary>
    [HttpGet( "mine" )]
    public async Task<ActionResult<List<AssessmentSubmissionDto>>> GetMySubmissions()
    {
        var submissions = await _db.AssessmentSubmissions
            .Where( s => s.UserId == UserId )
            .Include( s => s.AssessmentDefinition )
            .OrderByDescending( s => s.SubmittedAt )
            .Select( s => new AssessmentSubmissionDto(
                s.Id ,
                s.AssessmentDefinitionId ,
                s.AssessmentDefinition.Title ,
                s.AnswersJson ,
                s.SummaryJson ,
                s.SubmittedAt
            ) )
            .ToListAsync();

        return Ok( submissions );
    }
}
