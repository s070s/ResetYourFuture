using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Pages;

public partial class AdminAssessments
{
    [Inject] private IAdminAssessmentConsumer AssessmentConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private PagedResult<AssessmentDefinitionListItemDto>? _pagedResult;
    private int _page = 1;
    private int _pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50, 100];
    private string message = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadAssessments();
    }

    private async Task LoadAssessments()
    {
        try
        {
            _pagedResult = await AssessmentConsumer.GetAssessmentsAsync( _page , _pageSize );
        }
        catch ( Exception ex )
        {
            message = $"Error loading assessments: {ex.Message}";
        }
    }

    private async Task OnPageSizeChanged( ChangeEventArgs e )
    {
        if ( int.TryParse( e.Value?.ToString(), out var size ) )
        {
            _pageSize = size;
            _page = 1;
            await LoadAssessments();
        }
    }

    private async Task GoToPage( int page )
    {
        _page = page;
        await LoadAssessments();
    }

    private void CreateAssessment()
    {
        Nav.NavigateTo( "/admin/assessments/new" );
    }

    private void EditAssessment( Guid id )
    {
        Nav.NavigateTo( $"/admin/assessments/{id}/edit" );
    }

    private async Task PublishAssessment( Guid id )
    {
        try
        {
            if ( await AssessmentConsumer.PublishAssessmentAsync( id ) )
            {
                await LoadAssessments();
                message = "Assessment published";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task UnpublishAssessment( Guid id )
    {
        try
        {
            if ( await AssessmentConsumer.UnpublishAssessmentAsync( id ) )
            {
                await LoadAssessments();
                message = "Assessment unpublished";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private void ViewSubmissions( Guid id )
    {
        Nav.NavigateTo( $"/admin/assessments/{id}/submissions" );
    }

    private async Task DeleteAssessment( Guid id )
    {
        if ( !await JSRuntime.InvokeAsync<bool>( "confirm" , "Are you sure you want to delete this assessment?" ) )
            return;

        try
        {
            if ( await AssessmentConsumer.DeleteAssessmentAsync( id ) )
            {
                await LoadAssessments();
                message = "Assessment deleted";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }
}
