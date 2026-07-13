using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using System.Globalization;
using System.Text.Json;

namespace ResetYourFuture.Web.Pages;

public partial class AssessmentForm
{
    [Parameter]
    public Guid AssessmentId
    {
        get; set;
    }

    [Inject] private IAssessmentConsumer AssessmentConsumer { get; set; } = default!;
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ILogger<AssessmentForm> _logger { get; set; } = default!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private AssessmentDefinitionDto? assessment;
    private List<QuestionSchema>? questions;
    private Dictionary<string, string> answers = new();
    private bool isSubmitting = false;
    private bool submitted = false;
    private bool _loading = true;
    private string? _loadError;
    private string? _submitError;
    private HashSet<string> _missingRequiredIds = new();

    protected override async Task OnInitializedAsync()
    {
        var status = await SubscriptionService.GetStatusAsync();
        if (status?.Features?.AssessmentAccess != true)
        {
            Nav.NavigateTo("/pricing");
            return;
        }

        try
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";
            assessment = await AssessmentConsumer.GetAssessmentAsync(AssessmentId, lang);
            if (assessment != null)
            {
                var schema = JsonSerializer.Deserialize<AssessmentSchema>(assessment.SchemaJson, JsonOptions);
                questions = schema?.Questions ?? new List<QuestionSchema>();

                // Initialize answers dictionary
                foreach (var q in questions)
                {
                    answers[q.Id] = string.Empty;
                }
            }
            else
            {
                _loadError = AssessmentRes.AssessmentNotFound;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading assessment {AssessmentId}.", AssessmentId);
            _loadError = AssessmentRes.FailedToLoadAssessment;
        }
        finally
        {
            _loading = false;
        }
    }

    private void GoBack() => Nav.NavigateTo("/assessments");

    private void ToggleMultiSelect(string questionId, string option, bool isChecked)
    {
        var current = answers.ContainsKey(questionId) ? answers[questionId] : string.Empty;
        var selected = string.IsNullOrEmpty(current) ? new List<string>() : current.Split(',').ToList();

        if (isChecked && !selected.Contains(option))
        {
            selected.Add(option);
        }
        else if (!isChecked && selected.Contains(option))
        {
            selected.Remove(option);
        }

        answers[questionId] = string.Join(",", selected);
    }

    private async Task HandleSubmit()
    {
        _submitError = null;
        _missingRequiredIds = (questions ?? [])
            .Where(q => q.Required && string.IsNullOrWhiteSpace(answers.GetValueOrDefault(q.Id)))
            .Select(q => q.Id)
            .ToHashSet();

        if (_missingRequiredIds.Count > 0)
        {
            _submitError = AssessmentRes.RequiredQuestionMissing;
            return;
        }

        isSubmitting = true;
        try
        {
            var answersJson = JsonSerializer.Serialize(answers);
            var request = new SubmitAssessmentRequest(answersJson, null);

            var submission = await AssessmentConsumer.SubmitAssessmentAsync(AssessmentId, request);

            if (submission is not null)
            {
                submitted = true;
            }
            else
            {
                _logger.LogError("Assessment submission {AssessmentId} returned null.", AssessmentId);
                _submitError = AssessmentRes.SubmitFailed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting assessment {AssessmentId}.", AssessmentId);
            _submitError = AssessmentRes.SubmitFailed;
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private void ViewHistory()
    {
        Nav.NavigateTo("/assessments/mine");
    }

    private class AssessmentSchema
    {
        public List<QuestionSchema> Questions { get; set; } = new();
    }

    private class QuestionSchema
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
        public string Type { get; set; } = string.Empty;
        public List<string>? Options
        {
            get; set;
        }
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 5;
        public bool Required
        {
            get; set;
        }

        /// <summary>Uses Label if present, falls back to Text.</summary>
        public string DisplayLabel => Label ?? Text ?? string.Empty;
    }
}
