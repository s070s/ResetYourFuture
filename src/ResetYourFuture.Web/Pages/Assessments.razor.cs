using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class Assessments : IDisposable
{
    [Inject] private IAssessmentConsumer AssessmentService { get; set; } = default!;
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private ICategoryConsumer CategoryConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ILogger<Assessments> _logger { get; set; } = default!;

    private PagedResult<AssessmentDefinitionDto>? _pagedResult;
    private bool _assessmentAccess = false;
    private bool _loading = true;
    private string? _error;

    private int _page = 1;
    private int _pageSize = 10;

    private List<CategoryDto> _categories = [];
    private Guid? _selectedCategoryId;
    private string _search = string.Empty;
    private CancellationTokenSource? _searchCts;

    private static readonly int[] PageSizeOptions = [5, 10, 20, 50];

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    private bool HasActiveFilter => _selectedCategoryId is not null || !string.IsNullOrWhiteSpace(_search);

    protected override async Task OnInitializedAsync()
    {
        var status = await SubscriptionService.GetStatusAsync();
        _assessmentAccess = status?.Features?.AssessmentAccess == true;

        if (_assessmentAccess)
        {
            await Task.WhenAll(LoadAssessmentsAsync(), LoadCategoriesAsync());
        }
        else
        {
            _loading = false;
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            _categories = await CategoryConsumer.GetCategoriesAsync("assessments", CurrentLang);
        }
        catch (Exception ex)
        {
            _categories = [];
            _logger.LogError(ex, "Failed to load assessment categories.");
        }
    }

    private async Task LoadAssessmentsAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _pagedResult = await AssessmentService.GetAssessmentsAsync(_page, _pageSize, CurrentLang, _selectedCategoryId, _search);
        }
        catch (Exception ex)
        {
            _error = AssessmentRes.FailedToLoad;
            _logger.LogError(ex, "Failed to load assessments.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task GoToPage(int page)
    {
        if (page < 1 || (_pagedResult is not null && page > _pagedResult.TotalPages))
            return;
        _page = page;
        await LoadAssessmentsAsync();
    }

    private async Task ChangePageSize(int newSize)
    {
        _pageSize = newSize;
        _page = 1;
        await LoadAssessmentsAsync();
    }

    private async Task OnCategorySelected(Guid? categoryId)
    {
        _selectedCategoryId = categoryId;
        _page = 1;
        await LoadAssessmentsAsync();
    }

    private async Task OnSearchChanged(string value)
    {
        _search = value;
        _page = 1;

        var previous = _searchCts;
        _searchCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(300, _searchCts.Token);
            await LoadAssessmentsAsync();
        }
        catch (OperationCanceledException) { }
    }

    private void StartAssessment(Guid id)
    {
        Nav.NavigateTo($"/assessments/{id}");
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
