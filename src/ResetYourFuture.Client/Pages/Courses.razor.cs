using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.Courses;
using ResetYourFuture.Shared.Subscriptions;

namespace ResetYourFuture.Client.Pages;

public partial class Courses
{
    [Inject] private ICourseService CourseService { get; set; } = default!;
    [Inject] private ISubscriptionService SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private List<CourseListItemDto> _courses = [];
    private SubscriptionTier _userTier = SubscriptionTier.Free;
    private bool _loading = true;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var coursesTask = CourseService.GetCoursesAsync();
            var statusTask = SubscriptionService.GetStatusAsync();
            await Task.WhenAll(coursesTask, statusTask);

            _courses = coursesTask.Result;
            var status = statusTask.Result;
            if (status is not null)
            {
                _userTier = status.Tier;
            }
        }
        catch (Exception ex)
        {
            _error = "Failed to load courses. Please try again.";
            Console.WriteLine(ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ViewCourse(CourseListItemDto course)
    {
        if (_userTier < course.RequiredTier)
        {
            Navigation.NavigateTo("/pricing");
            return;
        }
        Navigation.NavigateTo($"/courses/{course.Id}");
    }

    private void GoToPricing()
    {
        Navigation.NavigateTo("/pricing");
    }
}
