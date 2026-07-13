using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Web.Consumers;

namespace ResetYourFuture.Web.Pages;

public partial class AdminCourseReviews
{
    [Inject] private IAdminCourseReviewConsumer Consumer { get; set; } = default!;

    private PagedResult<AdminCourseReviewDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string _sortBy = "createdat";
    private string _sortDir = "desc";
    private string _statusFilter = "Pending";
    private string message = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        pagedResult = await Consumer.GetAllAsync(currentPage, pageSize, _sortBy, _sortDir, _statusFilter);
    }

    private async Task OnStatusFilterChanged(ChangeEventArgs e)
    {
        _statusFilter = e.Value?.ToString() ?? string.Empty;
        currentPage = 1;
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

    private async Task ApproveAsync(Guid id)
    {
        if (await Consumer.ApproveAsync(id))
        {
            message = ReviewRes.Approve;
            await LoadAsync();
        }
    }

    private async Task RejectAsync(Guid id)
    {
        if (await Consumer.RejectAsync(id))
        {
            message = ReviewRes.Reject;
            await LoadAsync();
        }
    }
}
