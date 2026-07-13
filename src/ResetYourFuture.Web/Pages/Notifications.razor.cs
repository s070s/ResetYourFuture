using Microsoft.AspNetCore.Components;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Consumers;

namespace ResetYourFuture.Web.Pages;

public partial class Notifications
{
    [Inject] private INotificationConsumer Consumer { get; set; } = default!;

    private PagedResult<NotificationDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string _sortBy = "createdat";
    private string _sortDir = "desc";

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
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

    private async Task LoadAsync()
    {
        pagedResult = await Consumer.GetNotificationsAsync(currentPage, pageSize, _sortBy, _sortDir);
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

    private async Task MarkReadAsync(Guid id)
    {
        if (await Consumer.MarkReadAsync(id))
            await LoadAsync();
    }

    private async Task MarkAllReadAsync()
    {
        if (await Consumer.MarkAllReadAsync())
            await LoadAsync();
    }
}
