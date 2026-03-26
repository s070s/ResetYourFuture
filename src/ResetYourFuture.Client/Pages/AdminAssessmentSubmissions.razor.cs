using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminAssessmentSubmissions
{
    [Parameter]
    public Guid AssessmentId
    {
        get; set;
    }

    [Inject] private IAdminAssessmentConsumer AssessmentConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private PagedResult<AssessmentSubmissionListItemDto>? _pagedResult;
    private int _page = 1;
    private int _pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50, 100];
    private string errorMessage = string.Empty;
    private Guid? expandedSubmissionId;
    private string? assessmentTitle;

    /// <summary>Maps question id → label from the assessment schema.</summary>
    private Dictionary<string , string> questionLabels = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Load the assessment definition once to get schema + title
            var definition = await AssessmentConsumer.GetAssessmentAsync( AssessmentId );

            if ( definition != null )
            {
                assessmentTitle = definition.TitleEn;
                BuildQuestionLabels( definition.SchemaJson );
            }
        }
        catch ( Exception ex )
        {
            errorMessage = $"Error loading assessment: {ex.Message}";
        }

        await LoadSubmissions();
    }

    private async Task LoadSubmissions()
    {
        try
        {
            _pagedResult = await AssessmentConsumer.GetSubmissionsAsync( AssessmentId , _page , _pageSize )
                ?? new PagedResult<AssessmentSubmissionListItemDto>( [] , 0 , _page , _pageSize );
        }
        catch ( Exception ex )
        {
            errorMessage = $"Error loading submissions: {ex.Message}";
            _pagedResult = new PagedResult<AssessmentSubmissionListItemDto>( [] , 0 , _page , _pageSize );
        }
    }

    private async Task OnPageSizeChanged( int size )
    {
        _pageSize = size;
        _page = 1;
        await LoadSubmissions();
    }

    private async Task GoToPage( int page )
    {
        _page = page;
        await LoadSubmissions();
    }

    private void BuildQuestionLabels( string schemaJson )
    {
        try
        {
            var schema = JsonSerializer.Deserialize<SchemaRoot>( schemaJson , JsonOptions );
            if ( schema?.Questions != null )
            {
                foreach ( var q in schema.Questions )
                {
                    questionLabels [ q.Id ] = q.Label ?? q.Text ?? q.Id;
                }
            }
        }
        catch
        {
            // Schema could not be parsed; labels will fall back to question IDs
        }
    }

    private string ResolveLabel( string questionId )
        => questionLabels.TryGetValue( questionId , out var label ) ? label : questionId;

    private static Dictionary<string , string> ParseAnswers( string answersJson )
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string , string>>( answersJson , JsonOptions )
                   ?? new();
        }
        catch
        {
            return new();
        }
    }

    private void ToggleAnswers( Guid submissionId )
    {
        expandedSubmissionId = expandedSubmissionId == submissionId ? null : submissionId;
    }

    private void GoBack()
    {
        Nav.NavigateTo( "/admin/assessments" );
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
