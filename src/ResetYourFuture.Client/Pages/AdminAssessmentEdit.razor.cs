using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Client.Shared;
using ResetYourFuture.Shared.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminAssessmentEdit
{
    [Parameter]
    public Guid AssessmentId
    {
        get; set;
    }

    [Inject] private IAdminAssessmentConsumer AssessmentConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool IsNew => AssessmentId == Guid.Empty;
    private bool loading = true;
    private bool isSaving;
    private string message = string.Empty;

    private string assessmentKey = string.Empty;
    private string assessmentTitleEn = string.Empty;
    private string? assessmentTitleEl;
    private string? assessmentDescriptionEn;
    private string? assessmentDescriptionEl;
    private QuillEditor? descriptionEditorEn;
    private QuillEditor? descriptionEditorEl;
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
            var assessment = await AssessmentConsumer.GetAssessmentAsync( AssessmentId );
            if ( assessment != null )
            {
                assessmentKey = assessment.Key;
                assessmentTitleEn = assessment.TitleEn;
                assessmentTitleEl = assessment.TitleEl;
                assessmentDescriptionEn = assessment.DescriptionEn;
                assessmentDescriptionEl = assessment.DescriptionEl;
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
                        LabelEn = qEl.TryGetProperty( "labelEn" , out var labelEnEl ) ? labelEnEl.GetString() ?? ""
                            : ( qEl.TryGetProperty( "label" , out var labelEl ) ? labelEl.GetString() ?? "" : "" ) ,
                        LabelEl = qEl.TryGetProperty( "labelEl" , out var labelElEl ) ? labelElEl.GetString() : null ,
                        Required = qEl.TryGetProperty( "required" , out var reqEl ) ? reqEl.GetBoolean().ToString().ToLowerInvariant() : "false"
                    };

                    if ( q.Type == "rating" )
                    {
                        q.Min = qEl.TryGetProperty( "min" , out var minEl ) ? minEl.GetInt32() : 1;
                        q.Max = qEl.TryGetProperty( "max" , out var maxEl ) ? maxEl.GetInt32() : 5;
                    }

                    if ( q.Type == "choice" )
                    {
                        if ( qEl.TryGetProperty( "optionsEn" , out var optEnEl ) )
                        {
                            var options = new List<string>();
                            foreach ( var opt in optEnEl.EnumerateArray() )
                                options.Add( opt.GetString() ?? "" );
                            q.OptionsTextEn = string.Join( "\n" , options );
                        }
                        else if ( qEl.TryGetProperty( "options" , out var optEl ) )
                        {
                            var options = new List<string>();
                            foreach ( var opt in optEl.EnumerateArray() )
                                options.Add( opt.GetString() ?? "" );
                            q.OptionsTextEn = string.Join( "\n" , options );
                        }

                        if ( qEl.TryGetProperty( "optionsEl" , out var optElEl ) )
                        {
                            var options = new List<string>();
                            foreach ( var opt in optElEl.EnumerateArray() )
                                options.Add( opt.GetString() ?? "" );
                            q.OptionsTextEl = string.Join( "\n" , options );
                        }
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
            var descEn = descriptionEditorEn != null
                ? await descriptionEditorEn.GetContentAsync()
                : assessmentDescriptionEn;

            var descEl = descriptionEditorEl != null
                ? await descriptionEditorEl.GetContentAsync()
                : assessmentDescriptionEl;

            var schemaJson = GenerateSchemaJson();

            var request = new SaveAssessmentDefinitionRequest(
                assessmentKey ,
                assessmentTitleEn ,
                assessmentTitleEl ,
                descEn ,
                descEl ,
                schemaJson
            );

            AdminAssessmentDefinitionDto? result;
            if ( IsNew )
                result = await AssessmentConsumer.CreateAssessmentAsync( request );
            else
                result = await AssessmentConsumer.UpdateAssessmentAsync( AssessmentId , request );

            if ( result is not null )
                Nav.NavigateTo( "/admin/assessments" );
            else
                message = "Error saving assessment";
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

    private string GenerateSchemaJson()
    {
        var schema = new
        {
            id = assessmentKey ,
            title = assessmentTitleEn ,
            version = "1.0" ,
            questions = questions.Select( q =>
            {
                var dict = new Dictionary<string , object>
                {
                    [ "id" ] = q.Id ,
                    [ "type" ] = q.Type ,
                    [ "labelEn" ] = q.LabelEn ,
                    [ "required" ] = q.Required == "true"
                };

                if ( !string.IsNullOrEmpty( q.LabelEl ) )
                    dict [ "labelEl" ] = q.LabelEl;

                if ( q.Type == "rating" )
                {
                    dict [ "min" ] = q.Min;
                    dict [ "max" ] = q.Max;
                }

                if ( q.Type == "choice" )
                {
                    dict [ "optionsEn" ] = q.GetOptionsEn();
                    var optionsEl = q.GetOptionsEl();
                    if ( optionsEl.Count > 0 )
                        dict [ "optionsEl" ] = optionsEl;
                }

                return dict;
            } ).ToList()
        };

        return JsonSerializer.Serialize( schema , new JsonSerializerOptions { WriteIndented = true } );
    }

    private class QuestionModel
    {
        public string TempId { get; } = Guid.NewGuid().ToString( "N" );
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string LabelEn { get; set; } = string.Empty;
        public string? LabelEl
        {
            get; set;
        }
        public string Required { get; set; } = "false";
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 5;
        public string OptionsTextEn { get; set; } = string.Empty;
        public string? OptionsTextEl
        {
            get; set;
        }

        public List<string> GetOptionsEn() =>
            OptionsTextEn.Split( '\n' , StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ).ToList();

        public List<string> GetOptionsEl() =>
            ( OptionsTextEl ?? string.Empty ).Split( '\n' , StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ).ToList();
    }
}
