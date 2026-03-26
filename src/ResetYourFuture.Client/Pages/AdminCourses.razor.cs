using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Pages;

public partial class AdminCourses
{
    [Inject] private IAdminCourseConsumer CourseConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private PagedResult<AdminCourseDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string message = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadCourses();
    }

    private async Task LoadCourses()
    {
        try
        {
            pagedResult = await CourseConsumer.GetCoursesAsync( currentPage , pageSize );
        }
        catch ( Exception ex )
        {
            message = $"Error loading courses: {ex.Message}";
        }
    }

    private async Task OnPageSizeChanged( ChangeEventArgs e )
    {
        if ( int.TryParse( e.Value?.ToString() , out var size ) )
        {
            pageSize = size;
            currentPage = 1;
            await LoadCourses();
        }
    }

    private async Task PreviousPage()
    {
        if ( currentPage > 1 )
        {
            currentPage--;
            await LoadCourses();
        }
    }

    private async Task NextPage()
    {
        if ( pagedResult is { HasNextPage: true } )
        {
            currentPage++;
            await LoadCourses();
        }
    }

    private void CreateCourse()
    {
        Nav.NavigateTo( "/admin/courses/new" );
    }

    private void EditCourse( Guid id )
    {
        Nav.NavigateTo( $"/admin/courses/{id}" );
    }

    private async Task PublishCourse( Guid id )
    {
        try
        {
            if ( await CourseConsumer.PublishCourseAsync( id ) )
            {
                await LoadCourses();
                message = "Course published";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task UnpublishCourse( Guid id )
    {
        try
        {
            if ( await CourseConsumer.UnpublishCourseAsync( id ) )
            {
                await LoadCourses();
                message = "Course unpublished";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteCourse( Guid id )
    {
        if ( !await JSRuntime.InvokeAsync<bool>( "confirm" , "Are you sure you want to delete this course and all its modules/lessons?" ) )
            return;

        try
        {
            if ( await CourseConsumer.DeleteCourseAsync( id ) )
            {
                await LoadCourses();
                message = "Course deleted";
            }
            else
            {
                message = "Error deleting course";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }
}
