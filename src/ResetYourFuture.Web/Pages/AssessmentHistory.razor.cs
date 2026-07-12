using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;
using System.Globalization;
using System.Text.Json;

namespace ResetYourFuture.Web.Pages;

public partial class AssessmentHistory
{
    [Inject] private IAssessmentConsumer AssessmentConsumer { get; set; } = default!;
    [Inject] private ILogger<AssessmentHistory> _logger { get; set; } = default!;

    private PagedResult<AssessmentSubmissionDto>? pagedResult;
    private AssessmentSubmissionDto? latestSubmission;
    private AssessmentSubmissionDto? selectedSubmission;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string _sortBy = "submittedat";
    private string _sortDir = "desc";

    /// <summary>Cache of assessment schemas keyed by definition id → (questionId → label).</summary>
    private readonly Dictionary<Guid, Dictionary<string, string>> schemaCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadSubmissions();
        // The initial load is submittedat desc, so its first row is the latest submission;
        // captured once here, the card stays stable across later sorting/paging.
        latestSubmission = pagedResult?.Items.FirstOrDefault();
    }

    private async Task LoadSubmissions()
    {
        try
        {
            pagedResult = await AssessmentConsumer.GetMySubmissionsAsync(currentPage, pageSize, _sortBy, _sortDir)
                ?? new PagedResult<AssessmentSubmissionDto>([], 0, currentPage, pageSize);

            // Pre-load schemas for the assessments on this page so labels are available immediately
            foreach (var defId in pagedResult.Items.Select(s => s.AssessmentDefinitionId).Distinct())
            {
                await LoadSchemaAsync(defId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading assessment submissions.");
            pagedResult = new PagedResult<AssessmentSubmissionDto>([], 0, currentPage, pageSize);
        }
    }

    private async Task OnSort(string columnKey)
    {
        if (_sortBy == columnKey)
            _sortDir = _sortDir == "asc" ? "desc" : "asc";
        else
        {
            _sortBy = columnKey;
            _sortDir = "asc";
        }
        currentPage = 1;
        await LoadSubmissions();
    }

    private async Task OnPageSizeChanged(int size)
    {
        pageSize = size;
        currentPage = 1;
        await LoadSubmissions();
    }

    private async Task PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadSubmissions();
        }
    }

    private async Task NextPage()
    {
        if (pagedResult is { HasNextPage: true })
        {
            currentPage++;
            await LoadSubmissions();
        }
    }

    private async Task LoadSchemaAsync(Guid definitionId)
    {
        if (schemaCache.ContainsKey(definitionId))
            return;
        try
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";
            var def = await AssessmentConsumer.GetAssessmentAsync(definitionId, lang);
            if (def != null)
            {
                var schema = JsonSerializer.Deserialize<SchemaRoot>(def.SchemaJson, JsonOptions);
                var labels = new Dictionary<string, string>();
                if (schema?.Questions != null)
                {
                    foreach (var q in schema.Questions)
                    {
                        labels[q.Id] = q.Label ?? q.Text ?? q.Id;
                    }
                }
                schemaCache[definitionId] = labels;
            }
        }
        catch
        {
            schemaCache[definitionId] = new();
        }
    }

    private string ResolveLabel(Guid definitionId, string questionId)
    {
        if (schemaCache.TryGetValue(definitionId, out var labels)
            && labels.TryGetValue(questionId, out var label))
        {
            return label;
        }
        return questionId;
    }

    private static Dictionary<string, string> ParseAnswers(string answersJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson, JsonOptions) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private void ViewSubmission(Guid id)
    {
        selectedSubmission = pagedResult?.Items.FirstOrDefault(s => s.Id == id)
            ?? (latestSubmission?.Id == id ? latestSubmission : null);
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
