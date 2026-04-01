using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminCourses
{
    [Inject] private IAdminCourseConsumer CourseConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private PagedResult<AdminCourseDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string message = string.Empty;
    private Guid? _pendingDeleteId;

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

    private async Task OnPageSizeChanged( int size )
    {
        pageSize = size;
        currentPage = 1;
        await LoadCourses();
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

    private void DeleteCourse( Guid id )
    {
        _pendingDeleteId = id;
    }

    private async Task ExecuteDeleteAsync()
    {
        if ( _pendingDeleteId is not { } id )
            return;

        _pendingDeleteId = null;

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
