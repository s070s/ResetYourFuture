using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Application.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class Courses : IDisposable
{
    [Inject] private ICourseConsumer CourseService { get; set; } = default!;
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private ICategoryConsumer CategoryConsumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<Courses> _logger { get; set; } = default!;

    private PagedResult<CourseListItemDto>? _pagedResult;
    private UserSubscriptionStatusDto? _userStatus;
    private SubscriptionTier _userTier = SubscriptionTier.Free;
    private int _maxCourses = 1;
    private int _enrolledCount;
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
        var statusTask = SubscriptionService.GetStatusAsync();
        var categoriesTask = LoadCategoriesAsync();
        await Task.WhenAll(LoadCoursesAsync(), statusTask, categoriesTask);

        _userStatus = await statusTask;
        if (_userStatus is not null)
        {
            _userTier = _userStatus.Tier;
            _maxCourses = _userStatus.Features?.MaxCourses ?? 1;
        }

        UpdateEnrolledCount();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            _categories = await CategoryConsumer.GetCategoriesAsync("courses", CurrentLang);
        }
        catch (Exception ex)
        {
            _categories = [];
            _logger.LogError(ex, "Failed to load course categories.");
        }
    }

    private async Task LoadCoursesAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _pagedResult = await CourseService.GetCoursesAsync(_page, _pageSize, CurrentLang, _selectedCategoryId, _search);
            UpdateEnrolledCount();
        }
        catch (Exception ex)
        {
            _error = ErrorMessagesRes.FailedToLoadCourses;
            _logger.LogError(ex, "Failed to load courses.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void UpdateEnrolledCount()
    {
        _enrolledCount = _pagedResult?.Items.Count(c => c.IsEnrolled) ?? 0;
    }

    private async Task GoToPage(int page)
    {
        if (page < 1 || (_pagedResult is not null && page > _pagedResult.TotalPages))
            return;
        _page = page;
        await LoadCoursesAsync();
    }

    private async Task ChangePageSize(int newSize)
    {
        _pageSize = newSize;
        _page = 1;
        await LoadCoursesAsync();
    }

    private async Task OnCategorySelected(Guid? categoryId)
    {
        _selectedCategoryId = categoryId;
        _page = 1;
        await LoadCoursesAsync();
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
            await LoadCoursesAsync();
        }
        catch (OperationCanceledException) { }
    }

    private void ViewCourse(CourseListItemDto course)
    {
        // Enrolled courses are always accessible, even after a plan downgrade
        if (_userTier < course.RequiredTier && !course.IsEnrolled)
        {
            Navigation.NavigateTo("/pricing");
            return;
        }
        Navigation.NavigateTo($"/courses/{course.Id}");
    }

    private void GoToPricing() => Navigation.NavigateTo("/pricing");

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
