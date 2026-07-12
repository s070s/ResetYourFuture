using Microsoft.AspNetCore.Components;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Web.Consumers;

namespace ResetYourFuture.Web.Pages;

public partial class AdminCategories
{
    [Inject] private IAdminCategoryConsumer CategoryConsumer { get; set; } = default!;

    private PagedResult<AdminCategoryDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50, 100];
    private string message = string.Empty;

    private bool _formModalVisible;
    private Guid? _editingId;
    private string _formNameEn = string.Empty;
    private string? _formNameEl;
    private bool _isSaving;
    private string? _formError;

    private AdminCategoryDto? _pendingDelete;
    private string _sortBy = "nameen";
    private string _sortDir = "asc";

    protected override async Task OnInitializedAsync()
    {
        await LoadCategories();
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
        await LoadCategories();
    }

    private async Task LoadCategories()
    {
        try
        {
            pagedResult = await CategoryConsumer.GetCategoriesAsync(currentPage, pageSize, _sortBy, _sortDir);
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task OnPageSizeChanged(int size)
    {
        pageSize = size;
        currentPage = 1;
        await LoadCategories();
    }

    private async Task PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadCategories();
        }
    }

    private async Task NextPage()
    {
        if (pagedResult is { HasNextPage: true })
        {
            currentPage++;
            await LoadCategories();
        }
    }

    private void ShowAddModal()
    {
        _editingId = null;
        _formNameEn = string.Empty;
        _formNameEl = null;
        _formError = null;
        _formModalVisible = true;
    }

    private void ShowEditModal(AdminCategoryDto category)
    {
        _editingId = category.Id;
        _formNameEn = category.NameEn;
        _formNameEl = category.NameEl;
        _formError = null;
        _formModalVisible = true;
    }

    private void CloseFormModal() => _formModalVisible = false;

    private async Task SubmitForm()
    {
        _isSaving = true;
        _formError = null;
        try
        {
            var request = new SaveCategoryRequest(_formNameEn, _formNameEl);
            var result = _editingId is { } id
                ? await CategoryConsumer.UpdateCategoryAsync(id, request)
                : await CategoryConsumer.CreateCategoryAsync(request);

            if (result is not null)
            {
                _formModalVisible = false;
                await LoadCategories();
                message = CategoryRes.CategorySaved;
            }
            else
            {
                _formError = CategoryRes.ErrorSavingCategory;
            }
        }
        catch (Exception ex)
        {
            _formError = $"Error: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void RequestDelete(AdminCategoryDto category) => _pendingDelete = category;

    private void CancelDelete() => _pendingDelete = null;

    private async Task ConfirmDelete()
    {
        if (_pendingDelete is null)
            return;

        var id = _pendingDelete.Id;
        _pendingDelete = null;

        try
        {
            var success = await CategoryConsumer.DeleteCategoryAsync(id);
            message = success ? CategoryRes.CategoryDeleted : CategoryRes.ErrorDeletingCategory;
            if (success)
                await LoadCategories();
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
        }
    }
}
