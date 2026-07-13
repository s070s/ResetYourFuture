using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Consumers;

namespace ResetYourFuture.Web.Pages;

public partial class AdminLearningPathEditor
{
    [Parameter] public Guid? Id { get; set; }

    [Inject] private IAdminLearningPathConsumer PathConsumer { get; set; } = default!;
    [Inject] private IAdminCourseConsumer CourseConsumer { get; set; } = default!;
    [Inject] private IAdminCategoryConsumer CategoryConsumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool _isEditMode => Id.HasValue;
    private bool _isBusy;
    private string? _error;

    private string _titleEn = string.Empty;
    private string? _titleEl;
    private string? _descriptionEn;
    private string? _descriptionEl;
    private Guid? _categoryId;
    private int _displayOrder = 1;

    private List<CategoryOptionDto> _categories = [];
    private List<AdminLearningPathStepDto> _steps = [];
    private List<AdminCourseDto> _allCourses = [];
    private Guid? _selectedCourseIdToAdd;

    private IEnumerable<AdminCourseDto> AvailableCoursesToAdd =>
        _allCourses.Where(c => _steps.All(s => s.CourseId != c.Id));

    protected override async Task OnParametersSetAsync()
    {
        _error = null;

        var categoriesTask = LoadCategoriesAsync();
        var coursesTask = LoadCoursesAsync();

        if (_isEditMode)
            await LoadPathAsync();

        await Task.WhenAll(categoriesTask, coursesTask);
    }

    private async Task LoadCategoriesAsync()
    {
        try { _categories = await CategoryConsumer.GetAllCategoriesAsync(); }
        catch { _categories = []; }
    }

    private async Task LoadCoursesAsync()
    {
        try { _allCourses = (await CourseConsumer.GetCoursesAsync(1, 200, "titleen", "asc"))?.Items.ToList() ?? []; }
        catch { _allCourses = []; }
    }

    private async Task LoadPathAsync()
    {
        var path = await PathConsumer.GetByIdAsync(Id!.Value);
        if (path is null)
            return;

        _titleEn = path.TitleEn;
        _titleEl = path.TitleEl;
        _descriptionEn = path.DescriptionEn;
        _descriptionEl = path.DescriptionEl;
        _categoryId = path.CategoryId;
        _displayOrder = path.DisplayOrder;
        _steps = [.. path.Steps.OrderBy(s => s.StepOrder)];
    }

    private void OnCategoryChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        _categoryId = string.IsNullOrEmpty(value) ? null : Guid.Parse(value);
    }

    private void OnCourseToAddChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        _selectedCourseIdToAdd = string.IsNullOrEmpty(value) ? null : Guid.Parse(value);
    }

    private async Task Save()
    {
        _error = null;

        if (string.IsNullOrWhiteSpace(_titleEn))
        {
            _error = "Title (English) is required.";
            return;
        }

        _isBusy = true;
        try
        {
            var request = new SaveLearningPathRequest(
                _titleEn.Trim(),
                string.IsNullOrWhiteSpace(_titleEl) ? null : _titleEl.Trim(),
                string.IsNullOrWhiteSpace(_descriptionEn) ? null : _descriptionEn.Trim(),
                string.IsNullOrWhiteSpace(_descriptionEl) ? null : _descriptionEl.Trim(),
                _categoryId,
                _displayOrder);

            if (_isEditMode)
            {
                var updated = await PathConsumer.UpdateAsync(Id!.Value, request);
                if (updated is null)
                {
                    _error = "Failed to save the learning path.";
                    return;
                }
                await LoadPathAsync();
            }
            else
            {
                var created = await PathConsumer.CreateAsync(request);
                if (created is null)
                {
                    _error = "Failed to save the learning path.";
                    return;
                }
                Navigation.NavigateTo($"/admin/paths/{created.Id}");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void Cancel() => Navigation.NavigateTo("/admin/paths");

    private async Task AddStep()
    {
        if (_selectedCourseIdToAdd is not { } courseId || Id is not { } pathId)
            return;

        var result = await PathConsumer.AddStepAsync(pathId, courseId);
        if (result is not null)
        {
            _steps = [.. result.Steps.OrderBy(s => s.StepOrder)];
            _selectedCourseIdToAdd = null;
        }
    }

    private async Task RemoveStep(Guid stepId)
    {
        if (Id is not { } pathId)
            return;

        if (await PathConsumer.RemoveStepAsync(pathId, stepId))
            await LoadPathAsync();
    }

    private async Task MoveStepUp(Guid stepId)
    {
        if (Id is not { } pathId)
            return;

        if (await PathConsumer.MoveStepUpAsync(pathId, stepId))
            await LoadPathAsync();
    }

    private async Task MoveStepDown(Guid stepId)
    {
        if (Id is not { } pathId)
            return;

        if (await PathConsumer.MoveStepDownAsync(pathId, stepId))
            await LoadPathAsync();
    }
}
