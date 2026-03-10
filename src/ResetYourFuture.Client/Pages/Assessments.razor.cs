using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Shared.Resources;

namespace ResetYourFuture.Client.Pages;

public partial class Assessments
{
    [Inject] private IAssessmentService AssessmentService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private PagedResult<AssessmentDefinitionDto>? _pagedResult;
    private bool _loading = true;
    private string? _error;

    private int _page = 1;
    private int _pageSize = 10;

    private static readonly int[] PageSizeOptions = [5, 10, 20, 50];

    protected override async Task OnInitializedAsync()
    {
        await LoadAssessmentsAsync();
    }

    private async Task LoadAssessmentsAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _pagedResult = await AssessmentService.GetAssessmentsAsync( _page, _pageSize );
        }
        catch ( Exception ex )
        {
            _error = AssessmentRes.FailedToLoad;
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task GoToPage( int page )
    {
        if ( page < 1 || ( _pagedResult is not null && page > _pagedResult.TotalPages ) )
            return;
        _page = page;
        await LoadAssessmentsAsync();
    }

    private async Task ChangePageSize( int newSize )
    {
        _pageSize = newSize;
        _page = 1;
        await LoadAssessmentsAsync();
    }

    private void StartAssessment( Guid id )
    {
        Nav.NavigateTo( $"/assessments/{id}" );
    }
}
