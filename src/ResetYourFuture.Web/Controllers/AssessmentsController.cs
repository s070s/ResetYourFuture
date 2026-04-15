using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;
using System.Text.Json;

namespace ResetYourFuture.Web.Controllers;

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
    /// Resolves dual-language schema JSON to a flat single-language format for the student view.
    /// Handles both flat {"questions":[...]} and sectioned {"sections":[{"questions":[...]}]} schemas.
    /// Maps labelEn/labelEl or label/labelEl → label, and optionsEn/optionsEl or options/optionsEl → options.
    /// </summary>
    private string ResolveSchemaJsonByLang( string schemaJson , bool isEl )
    {
        try
        {
            using var doc = JsonDocument.Parse( schemaJson );
            var root = doc.RootElement;

            // Collect all question elements from either flat or sectioned schema formats.
            var allQuestions = new List<JsonElement>();
            if ( root.TryGetProperty( "questions" , out var flatQ ) && flatQ.ValueKind == JsonValueKind.Array )
            {
                foreach ( var q in flatQ.EnumerateArray() )
                    allQuestions.Add( q );
            }
            else if ( root.TryGetProperty( "sections" , out var sections ) && sections.ValueKind == JsonValueKind.Array )
            {
                foreach ( var section in sections.EnumerateArray() )
                {
                    if ( section.TryGetProperty( "questions" , out var sectionQ ) && sectionQ.ValueKind == JsonValueKind.Array )
                    {
                        foreach ( var q in sectionQ.EnumerateArray() )
                            allQuestions.Add( q );
                    }
                }
            }

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter( ms , new JsonWriterOptions { Indented = false } );
            writer.WriteStartObject();

            // Copy non-questions/sections root properties (id, title, version, etc.)
            foreach ( var prop in root.EnumerateObject() )
            {
                if ( prop.Name is "questions" or "sections" )
                    continue;
                prop.WriteTo( writer );
            }

            // Write resolved flat questions array
            writer.WritePropertyName( "questions" );
            writer.WriteStartArray();
            foreach ( var q in allQuestions )
            {
                writer.WriteStartObject();
                bool labelEmitted = false;
                bool optionsEmitted = false;

                foreach ( var qProp in q.EnumerateObject() )
                {
                    // Skip all Greek-only and secondary locale keys — handled below
                    if ( qProp.Name is "labelEl" or "optionsEl" or "titleEl" or "minLabelEl" or "maxLabelEl" )
                        continue;

                    // Resolve label: supports both "labelEn" (admin-edit) and "label" (seed) keys
                    if ( qProp.Name is "labelEn" or "label" )
                    {
                        if ( !labelEmitted )
                        {
                            var enLabel = qProp.Value.GetString() ?? "";
                            var elLabel = isEl && q.TryGetProperty( "labelEl" , out var elV ) ? elV.GetString() : null;
                            writer.WriteString( "label" , elLabel ?? enLabel );
                            labelEmitted = true;
                        }
                        continue;
                    }

                    // Resolve options: supports both "optionsEn" (admin-edit) and "options" (seed) keys
                    if ( qProp.Name is "optionsEn" or "options" )
                    {
                        if ( !optionsEmitted )
                        {
                            var useEl = isEl && q.TryGetProperty( "optionsEl" , out var elOpts ) && elOpts.GetArrayLength() > 0;
                            writer.WritePropertyName( "options" );
                            if ( useEl )
                                q.GetProperty( "optionsEl" ).WriteTo( writer );
                            else
                                qProp.Value.WriteTo( writer );
                            optionsEmitted = true;
                        }
                        continue;
                    }

                    qProp.WriteTo( writer );
                }

                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.Flush();
            return System.Text.Encoding.UTF8.GetString( ms.ToArray() );
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex , "Failed to resolve schema JSON by language; returning original." );
            return schemaJson;
        }
    }

    /// <summary>
    /// Get a paged list of published assessments.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AssessmentDefinitionDto>>> GetPublishedAssessments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string lang = "en" )
    {
        if ( page < 1 ) page = 1;
        if ( pageSize < 1 || pageSize > 100 ) pageSize = 10;

        var userStatus = await _subscriptionService.GetUserStatusAsync( UserId );
        if ( userStatus.Features?.AssessmentAccess != true )
            return StatusCode( 403 , "Assessment access requires a Plus subscription or higher." );

        var isEl = string.Equals( lang , "el" , StringComparison.OrdinalIgnoreCase );

        var query = _db.AssessmentDefinitions
            .AsNoTracking()
            .Where( a => a.IsPublished )
            .OrderBy( a => a.TitleEn );

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .Select( a => new AssessmentDefinitionDto(
                a.Id ,
                a.Key ,
                isEl ? ( a.TitleEl ?? a.TitleEn ) : a.TitleEn ,
                isEl ? ( a.DescriptionEl ?? a.DescriptionEn ) : a.DescriptionEn ,
                a.SchemaJson ,
                a.IsPublished ,
                a.CreatedAt ,
                a.UpdatedAt ,
                a.PublishedAt
            ) )
            .ToListAsync();

        // Resolve dual-language schema to single-language for student view
        var resolved = items.Select( a => a with { SchemaJson = ResolveSchemaJsonByLang( a.SchemaJson , isEl ) } ).ToList();

        return Ok( new PagedResult<AssessmentDefinitionDto>( resolved , totalCount , page , pageSize ) );
    }

    /// <summary>
    /// Get a specific published assessment by ID.
    /// </summary>
    [HttpGet( "{id:guid}" )]
    public async Task<ActionResult<AssessmentDefinitionDto>> GetAssessment( Guid id , [FromQuery] string lang = "en" )
    {
        var userStatus = await _subscriptionService.GetUserStatusAsync( UserId );
        if ( userStatus.Features?.AssessmentAccess != true )
            return StatusCode( 403 , "Assessment access requires a Plus subscription or higher." );

        var isEl = string.Equals( lang , "el" , StringComparison.OrdinalIgnoreCase );

        var assessment = await _db.AssessmentDefinitions
            .AsNoTracking()
            .Where( a => a.Id == id && a.IsPublished )
            .Select( a => new AssessmentDefinitionDto(
                a.Id ,
                a.Key ,
                isEl ? ( a.TitleEl ?? a.TitleEn ) : a.TitleEn ,
                isEl ? ( a.DescriptionEl ?? a.DescriptionEn ) : a.DescriptionEn ,
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

        // Resolve dual-language schema to single-language for student view
        var resolved = assessment with { SchemaJson = ResolveSchemaJsonByLang( assessment.SchemaJson , isEl ) };

        return Ok( resolved );
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

        // Check subscription features and tier
        var userStatus = await _subscriptionService.GetUserStatusAsync( UserId );
        if ( userStatus.Features?.AssessmentAccess != true )
            return StatusCode( 403 , "Assessment access requires a Plus subscription or higher." );
        if ( userStatus.Tier < assessment.RequiredTier )
            return StatusCode( 403 , $"This assessment requires a {assessment.RequiredTier} subscription or higher." );

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
            assessment.TitleEn ,
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
            .AsNoTracking()
            .Where( s => s.UserId == UserId )
            .Include( s => s.AssessmentDefinition )
            .OrderByDescending( s => s.SubmittedAt )
            .Select( s => new AssessmentSubmissionDto(
                s.Id ,
                s.AssessmentDefinitionId ,
                s.AssessmentDefinition.TitleEn ,
                s.AnswersJson ,
                s.SummaryJson ,
                s.SubmittedAt
            ) )
            .ToListAsync();

        return Ok( submissions );
    }
}
