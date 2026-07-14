using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminTestimonials
{
    [Inject] private IAdminTestimonialConsumer TestimonialConsumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private PagedResult<AdminTestimonialDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string message = string.Empty;
    private string messageType = "success";
    private Guid? confirmDeleteId;
    private string _sortBy = "displayorder";
    private string _sortDir = "asc";

    // Manual ↑/↓ reordering only makes sense while the table shows the curated
    // display order — any other sort disables the buttons (tooltip explains why).
    private bool IsDefaultSort => _sortBy == "displayorder" && _sortDir == "asc";

    protected override async Task OnInitializedAsync()
    {
        await LoadTestimonials();
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
        await LoadTestimonials();
    }

    private async Task LoadTestimonials()
    {
        pagedResult = await TestimonialConsumer.GetAllAsync(currentPage, pageSize, _sortBy, _sortDir);
    }

    private async Task OnPageSizeChanged(int size)
    {
        pageSize = size;
        currentPage = 1;
        await LoadTestimonials();
    }

    private async Task PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadTestimonials();
        }
    }

    private async Task NextPage()
    {
        if (pagedResult is { HasNextPage: true })
        {
            currentPage++;
            await LoadTestimonials();
        }
    }

    private void NewTestimonial() => Navigation.NavigateTo("/admin/testimonials/new");

    private void EditTestimonial(Guid id) => Navigation.NavigateTo($"/admin/testimonials/{id}");

    private async Task ToggleActive(Guid id)
    {
        try
        {
            var result = await TestimonialConsumer.ToggleActiveAsync(id);
            if (result is not null)
            {
                message = result.IsActive ? AdminRes.TestimonialActivated : AdminRes.TestimonialDeactivated;
                messageType = "success";
                await LoadTestimonials();
            }
            else
            {
                message = AdminRes.TestimonialStatusUpdateFailed;
                messageType = "danger";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task MoveUp(Guid id)
    {
        try
        {
            await TestimonialConsumer.MoveUpAsync(id);
            await LoadTestimonials();
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task MoveDown(Guid id)
    {
        try
        {
            await TestimonialConsumer.MoveDownAsync(id);
            await LoadTestimonials();
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task DeleteTestimonial(Guid id)
    {
        try
        {
            var success = await TestimonialConsumer.DeleteAsync(id);
            if (success)
            {
                confirmDeleteId = null;
                message = AdminRes.TestimonialDeleted;
                messageType = "success";
                await LoadTestimonials();
            }
            else
            {
                message = AdminRes.TestimonialDeleteFailed;
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
