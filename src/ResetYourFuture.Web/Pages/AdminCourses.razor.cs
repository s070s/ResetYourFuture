using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminCourses : IAsyncDisposable
{
    [Inject] private IAdminCourseConsumer CourseConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private PagedResult<AdminCourseDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string message = string.Empty;
    private string messageType = "success";
    private Guid? _pendingDeleteId;
    private string _sortBy = "createdat";
    private string _sortDir = "desc";
    private string searchTerm = string.Empty;
    private CancellationTokenSource? _searchCts;

    protected override async Task OnInitializedAsync()
    {
        await LoadCourses();
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
        await LoadCourses();
    }

    private async Task LoadCourses()
    {
        try
        {
            pagedResult = await CourseConsumer.GetCoursesAsync(
                currentPage, pageSize, _sortBy, _sortDir,
                string.IsNullOrEmpty(searchTerm) ? null : searchTerm);
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchTerm = e.Value?.ToString() ?? string.Empty;
        currentPage = 1;

        var previous = _searchCts;
        _searchCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(300, _searchCts.Token);
            await LoadCourses();
        }
        catch (OperationCanceledException) { }
    }

    public ValueTask DisposeAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task OnPageSizeChanged(int size)
    {
        pageSize = size;
        currentPage = 1;
        await LoadCourses();
    }

    private async Task PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadCourses();
        }
    }

    private async Task NextPage()
    {
        if (pagedResult is { HasNextPage: true })
        {
            currentPage++;
            await LoadCourses();
        }
    }

    private void CreateCourse()
    {
        Nav.NavigateTo("/admin/courses/new");
    }

    private void EditCourse(Guid id)
    {
        Nav.NavigateTo($"/admin/courses/{id}");
    }

    private async Task PublishCourse(Guid id)
    {
        try
        {
            if (await CourseConsumer.PublishCourseAsync(id))
            {
                await LoadCourses();
                message = AdminRes.CoursePublished;
                messageType = "success";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task UnpublishCourse(Guid id)
    {
        try
        {
            if (await CourseConsumer.UnpublishCourseAsync(id))
            {
                await LoadCourses();
                message = AdminRes.CourseUnpublished;
                messageType = "success";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private void DeleteCourse(Guid id)
    {
        _pendingDeleteId = id;
    }

    private async Task ExecuteDeleteAsync()
    {
        if (_pendingDeleteId is not { } id)
            return;

        _pendingDeleteId = null;

        try
        {
            if (await CourseConsumer.DeleteCourseAsync(id))
            {
                await LoadCourses();
                message = AdminRes.CourseDeleted;
                messageType = "success";
            }
            else
            {
                message = AdminRes.CourseDeleteFailed;
                messageType = "danger";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }
}
