using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;
using System.Text.Json;

namespace ResetYourFuture.Web.Pages;

public partial class AssessmentHistory
{
    [Inject] private IAssessmentConsumer AssessmentConsumer { get; set; } = default!;
    [Inject] private ILogger<AssessmentHistory> _logger { get; set; } = default!;

    private List<AssessmentSubmissionDto>? submissions;
    private List<AssessmentSubmissionDto> _sortedSubmissions = new();
    private AssessmentSubmissionDto? latestSubmission;
    private AssessmentSubmissionDto? selectedSubmission;

    /// <summary>Cache of assessment schemas keyed by definition id → (questionId → label).</summary>
    private readonly Dictionary<Guid , Dictionary<string , string>> schemaCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            submissions = await AssessmentConsumer.GetMySubmissionsAsync();
            latestSubmission = submissions?.OrderByDescending( s => s.SubmittedAt ).FirstOrDefault();
            _sortedSubmissions = submissions?.OrderByDescending( s => s.SubmittedAt ).ToList() ?? new();

            // Pre-load schemas for all distinct assessments so labels are available immediately
            if ( submissions != null )
            {
                var distinctIds = submissions.Select( s => s.AssessmentDefinitionId ).Distinct();
                foreach ( var defId in distinctIds )
                {
                    await LoadSchemaAsync( defId );
                }
            }
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex , "Error loading assessment submissions." );
            submissions = new List<AssessmentSubmissionDto>();
        }
    }

    private async Task LoadSchemaAsync( Guid definitionId )
    {
        if ( schemaCache.ContainsKey( definitionId ) )
            return;
        try
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";
            var def = await AssessmentConsumer.GetAssessmentAsync( definitionId, lang );
            if ( def != null )
            {
                var schema = JsonSerializer.Deserialize<SchemaRoot>( def.SchemaJson , JsonOptions );
                var labels = new Dictionary<string , string>();
                if ( schema?.Questions != null )
                {
                    foreach ( var q in schema.Questions )
                    {
                        labels [ q.Id ] = q.Label ?? q.Text ?? q.Id;
                    }
                }
                schemaCache [ definitionId ] = labels;
            }
        }
        catch
        {
            schemaCache [ definitionId ] = new();
        }
    }

    private string ResolveLabel( Guid definitionId , string questionId )
    {
        if ( schemaCache.TryGetValue( definitionId , out var labels )
            && labels.TryGetValue( questionId , out var label ) )
        {
            return label;
        }
        return questionId;
    }

    private static Dictionary<string , string> ParseAnswers( string answersJson )
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string , string>>( answersJson , JsonOptions ) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private void ViewSubmission( Guid id )
    {
        selectedSubmission = submissions?.FirstOrDefault( s => s.Id == id );
    }

    private void CloseModal()
    {
        selectedSubmission = null;
    }

    private class SchemaRoot
    {
        public List<SchemaQuestion> Questions { get; set; } = new();
    }

    private class SchemaQuestion
    {
        public string Id { get; set; } = string.Empty;
        public string? Text
        {
            get; set;
        }
        public string? Label
        {
            get; set;
        }
    }
}
