using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.Courses;

namespace ResetYourFuture.Client.Pages;

public partial class CourseDetail
{
    [Parameter]
    public Guid CourseId { get; set; }

    [Inject] private ICourseService CourseService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private CourseDetailDto? _course;
    private bool _loading = true;
    private bool _enrolling;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        await LoadCourse();
    }

    private async Task LoadCourse()
    {
        _loading = true;
        _error = null;

        try
        {
            _course = await CourseService.GetCourseAsync(CourseId);
            if (_course is null)
            {
                _error = "Course not found.";
            }
        }
        catch (Exception ex)
        {
            _error = "Failed to load course. Please try again.";
            Console.WriteLine(ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task EnrollInCourse()
    {
        _enrolling = true;
        try
        {
            var result = await CourseService.EnrollAsync(CourseId);
            if (result?.Success == true)
            {
                await LoadCourse();
            }
            else
            {
                _error = result?.Message ?? "Failed to enroll.";
            }
        }
        catch (Exception ex)
        {
            _error = "Failed to enroll. Please try again.";
            Console.WriteLine(ex.Message);
        }
        finally
        {
            _enrolling = false;
        }
    }

    private void ViewLesson(Guid lessonId)
    {
        if (_course?.IsEnrolled == true)
        {
            Navigation.NavigateTo($"/lessons/{lessonId}");
        }
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/courses");
    }
}
