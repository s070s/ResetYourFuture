using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Shared.Resources.Messages;
using Microsoft.AspNetCore.Components.Web;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Shared.Components.Forms;
using ResetYourFuture.Application.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Web.Pages;

public partial class AdminAssessmentEdit
{
    [Parameter]
    public Guid AssessmentId
    {
        get; set;
    }

    [Inject] private IAdminAssessmentConsumer AssessmentConsumer { get; set; } = default!;
    [Inject] private IAdminCategoryConsumer CategoryConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool IsNew => AssessmentId == Guid.Empty;
    private bool loading = true;
    private bool isSaving;
    private string message = string.Empty;
    private string messageType = "danger";
    private bool _isDirty;

    private void MarkDirty() => _isDirty = true;

    private string assessmentKey = string.Empty;
    private string assessmentTitleEn = string.Empty;
    private string? assessmentTitleEl;
    private string? assessmentDescriptionEn;
    private string? assessmentDescriptionEl;
    private QuillEditor? descriptionEditorEn;
    private QuillEditor? descriptionEditorEl;
    private List<QuestionModel> questions = new();
    private HashSet<string> _expandedQuestions = new();

    // Category picker state
    private List<CategoryOptionDto> categories = [];
    private Guid? assessmentCategoryId;
    private bool creatingNewCategory;
    private string? newCategoryName;

    protected override async Task OnInitializedAsync()
    {
        await LoadCategories();

        if (!IsNew)
        {
            await LoadAssessment();
        }
        loading = false;
        _isDirty = false;
    }

    private async Task LoadCategories()
    {
        try
        {
            categories = await CategoryConsumer.GetAllCategoriesAsync();
        }
        catch
        {
            categories = [];
        }
    }

    private async Task LoadAssessment()
    {
        try
        {
            var assessment = await AssessmentConsumer.GetAssessmentAsync(AssessmentId);
            if (assessment != null)
            {
                assessmentKey = assessment.Key;
                assessmentTitleEn = assessment.TitleEn;
                assessmentTitleEl = assessment.TitleEl;
                assessmentDescriptionEn = assessment.DescriptionEn;
                assessmentDescriptionEl = assessment.DescriptionEl;
                assessmentCategoryId = assessment.CategoryId;
                creatingNewCategory = false;
                newCategoryName = null;
                ParseSchemaToQuestions(assessment.SchemaJson);
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
        }
    }

    // ── Category picker ──

    private string CategorySelectValue => creatingNewCategory ? "new" : (assessmentCategoryId?.ToString() ?? "none");

    private void OnCategorySelectChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (value == "new")
        {
            creatingNewCategory = true;
            assessmentCategoryId = null;
        }
        else if (string.IsNullOrEmpty(value) || value == "none")
        {
            creatingNewCategory = false;
            assessmentCategoryId = null;
            newCategoryName = null;
        }
        else
        {
            creatingNewCategory = false;
            newCategoryName = null;
            assessmentCategoryId = Guid.Parse(value);
        }
    }

    private void ParseSchemaToQuestions(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;

            // Support both flat {"questions":[...]} and sectioned {"sections":[{"questions":[...]}]} schemas
            var questionElements = new List<JsonElement>();

            if (root.TryGetProperty("questions", out var flatQuestions))
            {
                foreach (var qEl in flatQuestions.EnumerateArray())
                    questionElements.Add(qEl);
            }
            else if (root.TryGetProperty("sections", out var sections))
            {
                foreach (var section in sections.EnumerateArray())
                {
                    if (section.TryGetProperty("questions", out var sectionQuestions))
                    {
                        foreach (var qEl in sectionQuestions.EnumerateArray())
                            questionElements.Add(qEl);
                    }
                }
            }

            foreach (var qEl in questionElements)
            {
                var rawType = qEl.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "text" : "text";
                // Normalise multiselect → choice; the editor treats them identically
                var editorType = rawType == "multiselect" ? "choice" : rawType;

                var q = new QuestionModel
                {
                    Id = qEl.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                    Type = editorType,
                    LabelEn = qEl.TryGetProperty("labelEn", out var labelEnEl) ? labelEnEl.GetString() ?? ""
                        : (qEl.TryGetProperty("label", out var labelEl) ? labelEl.GetString() ?? "" : ""),
                    LabelEl = qEl.TryGetProperty("labelEl", out var labelElEl) ? labelElEl.GetString() : null,
                    Required = qEl.TryGetProperty("required", out var reqEl) ? reqEl.GetBoolean().ToString().ToLowerInvariant() : "false"
                };

                if (q.Type == "rating")
                {
                    q.Min = qEl.TryGetProperty("min", out var minEl) ? minEl.GetInt32() : 1;
                    q.Max = qEl.TryGetProperty("max", out var maxEl) ? maxEl.GetInt32() : 5;
                }

                if (q.Type == "choice")
                {
                    if (qEl.TryGetProperty("optionsEn", out var optEnEl))
                    {
                        var options = new List<string>();
                        foreach (var opt in optEnEl.EnumerateArray())
                            options.Add(opt.GetString() ?? "");
                        q.OptionsTextEn = string.Join("\n", options);
                    }
                    else if (qEl.TryGetProperty("options", out var optEl))
                    {
                        var options = new List<string>();
                        foreach (var opt in optEl.EnumerateArray())
                            options.Add(opt.GetString() ?? "");
                        q.OptionsTextEn = string.Join("\n", options);
                    }

                    if (qEl.TryGetProperty("optionsEl", out var optElEl))
                    {
                        var options = new List<string>();
                        foreach (var opt in optElEl.EnumerateArray())
                            options.Add(opt.GetString() ?? "");
                        q.OptionsTextEl = string.Join("\n", options);
                    }
                }

                questions.Add(q);
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
        var q = new QuestionModel { Id = $"q{nextNum}" };
        questions.Add(q);
        _expandedQuestions.Add(q.TempId);
        _isDirty = true;
    }

    private void RemoveQuestion(int index)
    {
        if (index >= 0 && index < questions.Count)
        {
            _expandedQuestions.Remove(questions[index].TempId);
            questions.RemoveAt(index);
            _isDirty = true;
        }
    }

    private void ToggleQuestion(string tempId)
    {
        if (!_expandedQuestions.Remove(tempId))
            _expandedQuestions.Add(tempId);
    }

    private void MoveQuestion(int index, int direction)
    {
        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= questions.Count)
            return;
        (questions[index], questions[newIndex]) = (questions[newIndex], questions[index]);
        _isDirty = true;
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

            var categoryIdArg = creatingNewCategory ? null : assessmentCategoryId;
            var newCategoryNameArg = creatingNewCategory ? newCategoryName : null;

            var request = new SaveAssessmentDefinitionRequest(
                assessmentKey,
                assessmentTitleEn,
                assessmentTitleEl,
                descEn,
                descEl,
                schemaJson,
                categoryIdArg,
                newCategoryNameArg
            );

            AdminAssessmentDefinitionDto? result;
            if (IsNew)
                result = await AssessmentConsumer.CreateAssessmentAsync(request);
            else
                result = await AssessmentConsumer.UpdateAssessmentAsync(AssessmentId, request);

            if (result is not null)
            {
                _isDirty = false;
                Nav.NavigateTo("/admin/assessments");
            }
            else
                message = AdminRes.AssessmentSaveFailed;
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
        }
        finally
        {
            isSaving = false;
        }
    }

    private void GoBack()
    {
        Nav.NavigateTo("/admin/assessments");
    }

    private string GenerateSchemaJson()
    {
        var schema = new
        {
            id = assessmentKey,
            title = assessmentTitleEn,
            version = "1.0",
            questions = questions.Select(q =>
            {
                var dict = new Dictionary<string, object>
                {
                    ["id"] = q.Id,
                    ["type"] = q.Type,
                    ["labelEn"] = q.LabelEn,
                    ["required"] = q.Required == "true"
                };

                if (!string.IsNullOrEmpty(q.LabelEl))
                    dict["labelEl"] = q.LabelEl;

                if (q.Type == "rating")
                {
                    dict["min"] = q.Min;
                    dict["max"] = q.Max;
                }

                if (q.Type == "choice")
                {
                    dict["optionsEn"] = q.GetOptionsEn();
                    var optionsEl = q.GetOptionsEl();
                    if (optionsEl.Count > 0)
                        dict["optionsEl"] = optionsEl;
                }

                return dict;
            }).ToList()
        };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
    }

    private class QuestionModel
    {
        public string TempId { get; } = Guid.NewGuid().ToString("N");
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
            OptionsTextEn.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        public List<string> GetOptionsEl() =>
            (OptionsTextEl ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
