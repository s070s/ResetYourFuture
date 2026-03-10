using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminCourses
{
    [Inject] private HttpClient Http { get; set; } = default!;
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
            pagedResult = await Http.GetFromJsonAsync<PagedResult<AdminCourseDto>>(
                $"api/admin/courses?page={currentPage}&pageSize={pageSize}" );
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
            var response = await Http.PostAsync( $"api/admin/courses/{id}/publish" , null );
            if ( response.IsSuccessStatusCode )
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
            var response = await Http.PostAsync( $"api/admin/courses/{id}/unpublish" , null );
            if ( response.IsSuccessStatusCode )
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
            var response = await Http.DeleteAsync( $"api/admin/courses/{id}" );
            if ( response.IsSuccessStatusCode )
            {
                await LoadCourses();
                message = "Course deleted";
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                message = $"Error: {body}";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }
}
