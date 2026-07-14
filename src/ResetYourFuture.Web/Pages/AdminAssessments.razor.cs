using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminAssessments
{
    [Inject] private IAdminAssessmentConsumer AssessmentConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private PagedResult<AssessmentDefinitionListItemDto>? _pagedResult;
    private int _page = 1;
    private int _pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50, 100];
    private string message = string.Empty;
    private string messageType = "success";
    private Guid? _pendingDeleteId;
    private string _sortBy = "createdat";
    private string _sortDir = "desc";

    protected override async Task OnInitializedAsync()
    {
        await LoadAssessments();
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
        _page = 1;
        await LoadAssessments();
    }

    private async Task LoadAssessments()
    {
        try
        {
            _pagedResult = await AssessmentConsumer.GetAssessmentsAsync(_page, _pageSize, _sortBy, _sortDir);
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task OnPageSizeChanged(int size)
    {
        _pageSize = size;
        _page = 1;
        await LoadAssessments();
    }

    private async Task GoToPage(int page)
    {
        _page = page;
        await LoadAssessments();
    }

    private void CreateAssessment()
    {
        Nav.NavigateTo("/admin/assessments/new");
    }

    private void EditAssessment(Guid id)
    {
        Nav.NavigateTo($"/admin/assessments/{id}/edit");
    }

    private async Task PublishAssessment(Guid id)
    {
        try
        {
            if (await AssessmentConsumer.PublishAssessmentAsync(id))
            {
                await LoadAssessments();
                message = AdminRes.AssessmentPublished;
                messageType = "success";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task UnpublishAssessment(Guid id)
    {
        try
        {
            if (await AssessmentConsumer.UnpublishAssessmentAsync(id))
            {
                await LoadAssessments();
                message = AdminRes.AssessmentUnpublished;
                messageType = "success";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private void ViewSubmissions(Guid id)
    {
        Nav.NavigateTo($"/admin/assessments/{id}/submissions");
    }

    private void DeleteAssessment(Guid id)
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
            if (await AssessmentConsumer.DeleteAssessmentAsync(id))
            {
                await LoadAssessments();
                message = AdminRes.AssessmentDeleted;
                messageType = "success";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }
}
