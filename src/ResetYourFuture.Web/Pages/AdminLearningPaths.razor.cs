using Microsoft.AspNetCore.Components;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Web.Consumers;

namespace ResetYourFuture.Web.Pages;

public partial class AdminLearningPaths
{
    [Inject] private IAdminLearningPathConsumer Consumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private PagedResult<AdminLearningPathDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string _sortBy = "displayorder";
    private string _sortDir = "asc";
    private string message = string.Empty;
    private Guid? confirmDeleteId;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        pagedResult = await Consumer.GetAllAsync(currentPage, pageSize, _sortBy, _sortDir);
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
        await LoadAsync();
    }

    private async Task OnPageSizeChanged(int size)
    {
        pageSize = size;
        currentPage = 1;
        await LoadAsync();
    }

    private async Task PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadAsync();
        }
    }

    private async Task NextPage()
    {
        if (pagedResult is { HasNextPage: true })
        {
            currentPage++;
            await LoadAsync();
        }
    }

    private void NewPath() => Nav.NavigateTo("/admin/paths/new");

    private void EditPath(Guid id) => Nav.NavigateTo($"/admin/paths/{id}");

    private async Task Publish(Guid id)
    {
        if (await Consumer.PublishAsync(id))
        {
            message = PathRes.Publish;
            await LoadAsync();
        }
    }

    private async Task Unpublish(Guid id)
    {
        if (await Consumer.UnpublishAsync(id))
        {
            message = PathRes.Unpublish;
            await LoadAsync();
        }
    }

    private async Task DeletePath(Guid id)
    {
        confirmDeleteId = null;
        if (await Consumer.DeleteAsync(id))
        {
            message = PathRes.Delete;
            await LoadAsync();
        }
    }
}
