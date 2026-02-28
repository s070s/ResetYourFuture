using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ResetYourFuture.Client.Shared;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;
using System.Text.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminAssessmentEdit
{
    [Parameter]
    public Guid AssessmentId
    {
        get; set;
    }

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool IsNew => AssessmentId == Guid.Empty;
    private bool loading = true;
    private bool isSaving;
    private string message = string.Empty;

    private string assessmentKey = string.Empty;
    private string assessmentTitle = string.Empty;
    private string? assessmentDescription;
    private QuillEditor? descriptionEditor;
    private List<QuestionModel> questions = new();

    protected override async Task OnInitializedAsync()
    {
        if ( !IsNew )
        {
            await LoadAssessment();
        }
        loading = false;
    }

    private async Task LoadAssessment()
    {
        try
        {
            var assessment = await Http.GetFromJsonAsync<AssessmentDefinitionDto>(
                $"api/admin/assessments/{AssessmentId}" );
            if ( assessment != null )
            {
                assessmentKey = assessment.Key;
                assessmentTitle = assessment.Title;
                assessmentDescription = assessment.Description;
                ParseSchemaToQuestions( assessment.SchemaJson );
            }
        }
        catch ( Exception ex )
        {
            message = $"Error loading assessment: {ex.Message}";
        }
    }

    private void ParseSchemaToQuestions( string schemaJson )
    {
        try
        {
            using var doc = JsonDocument.Parse( schemaJson );
            var root = doc.RootElement;

            if ( root.TryGetProperty( "questions" , out var questionsElement ) )
            {
                foreach ( var qEl in questionsElement.EnumerateArray() )
                {
                    var q = new QuestionModel
                    {
                        Id = qEl.TryGetProperty( "id" , out var idEl ) ? idEl.GetString() ?? "" : "" ,
                        Type = qEl.TryGetProperty( "type" , out var typeEl ) ? typeEl.GetString() ?? "text" : "text" ,
                        Label = qEl.TryGetProperty( "label" , out var labelEl ) ? labelEl.GetString() ?? "" : "" ,
                        Required = qEl.TryGetProperty( "required" , out var reqEl ) ? reqEl.GetBoolean().ToString().ToLowerInvariant() : "false"
                    };

                    if ( q.Type == "rating" )
                    {
                        q.Min = qEl.TryGetProperty( "min" , out var minEl ) ? minEl.GetInt32() : 1;
                        q.Max = qEl.TryGetProperty( "max" , out var maxEl ) ? maxEl.GetInt32() : 5;
                    }

                    if ( q.Type == "choice" && qEl.TryGetProperty( "options" , out var optEl ) )
                    {
                        var options = new List<string>();
                        foreach ( var opt in optEl.EnumerateArray() )
                        {
                            options.Add( opt.GetString() ?? "" );
                        }
                        q.OptionsText = string.Join( "\n" , options );
                    }

                    questions.Add( q );
                }
            }
        }
        catch
        {
            // If parsing fails, start with empty questions
            questions = new List<QuestionModel>();
        }
    }

    private string GenerateSchemaJson()
    {
        var schema = new
        {
            id = assessmentKey ,
            title = assessmentTitle ,
            version = "1.0" ,
            questions = questions.Select( q =>
            {
                var dict = new Dictionary<string , object>
                {
                    [ "id" ] = q.Id ,
                    [ "type" ] = q.Type ,
                    [ "label" ] = q.Label ,
                    [ "required" ] = q.Required == "true"
                };

                if ( q.Type == "rating" )
                {
                    dict [ "min" ] = q.Min;
                    dict [ "max" ] = q.Max;
                }

                if ( q.Type == "choice" )
                {
                    dict [ "options" ] = q.GetOptions();
                }

                return dict;
            } ).ToList()
        };

        return JsonSerializer.Serialize( schema , new JsonSerializerOptions { WriteIndented = true } );
    }

    private void AddQuestion()
    {
        var nextNum = questions.Count + 1;
        questions.Add( new QuestionModel { Id = $"q{nextNum}" } );
    }

    private void RemoveQuestion( int index )
    {
        if ( index >= 0 && index < questions.Count )
        {
            questions.RemoveAt( index );
        }
    }

    private void MoveQuestion( int index , int direction )
    {
        var newIndex = index + direction;
        if ( newIndex < 0 || newIndex >= questions.Count )
            return;
        (questions [ index ] , questions [ newIndex ]) = (questions [ newIndex ] , questions [ index ]);
    }

    private async Task SaveAssessment()
    {
        isSaving = true;
        message = string.Empty;
        try
        {
            var desc = descriptionEditor != null
                ? await descriptionEditor.GetContentAsync()
                : assessmentDescription;

            var schemaJson = GenerateSchemaJson();

            var request = new SaveAssessmentDefinitionRequest(
                assessmentKey ,
                assessmentTitle ,
                desc ,
                schemaJson
            );

            HttpResponseMessage response;
            if ( IsNew )
            {
                response = await Http.PostAsJsonAsync( "api/admin/assessments" , request );
            }
            else
            {
                response = await Http.PutAsJsonAsync( $"api/admin/assessments/{AssessmentId}" , request );
            }

            if ( response.IsSuccessStatusCode )
            {
                Nav.NavigateTo( "/admin/assessments" );
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                message = $"Error saving: {body}";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
        finally
        {
            isSaving = false;
        }
    }

    private void GoBack()
    {
        Nav.NavigateTo( "/admin/assessments" );
    }

    private class QuestionModel
    {
        public string TempId { get; } = Guid.NewGuid().ToString( "N" );
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string Label { get; set; } = string.Empty;
        public string Required { get; set; } = "false";
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 5;
        public string OptionsText { get; set; } = string.Empty;

        public List<string> GetOptions() =>
            OptionsText.Split( '\n' , StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ).ToList();
    }
}
