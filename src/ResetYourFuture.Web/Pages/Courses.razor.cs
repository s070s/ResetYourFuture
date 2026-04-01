using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class Courses
{
    [Inject] private ICourseConsumer CourseService { get; set; } = default!;
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private PagedResult<CourseListItemDto>? _pagedResult;
    private UserSubscriptionStatusDto? _userStatus;
    private SubscriptionTierEnum _userTier = SubscriptionTierEnum.Free;
    private int _maxCourses = 1;
    private int _enrolledCount;
    private bool _loading = true;
    private string? _error;

    private int _page = 1;
    private int _pageSize = 10;

    private static readonly int[] PageSizeOptions = [5, 10, 20, 50];

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        var statusTask = SubscriptionService.GetStatusAsync();
        await Task.WhenAll( LoadCoursesAsync(), statusTask );

        _userStatus = statusTask.Result;
        if ( _userStatus is not null )
        {
            _userTier = _userStatus.Tier;
            _maxCourses = _userStatus.Features?.MaxCourses ?? 1;
        }

        UpdateEnrolledCount();
    }

    private async Task LoadCoursesAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _pagedResult = await CourseService.GetCoursesAsync( _page, _pageSize, CurrentLang );
            UpdateEnrolledCount();
        }
        catch ( Exception ex )
        {
            _error = "Failed to load courses. Please try again.";
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _loading = false;
        }
    }

    private void UpdateEnrolledCount()
    {
        _enrolledCount = _pagedResult?.Items.Count( c => c.IsEnrolled ) ?? 0;
    }

    private async Task GoToPage( int page )
    {
        if ( page < 1 || ( _pagedResult is not null && page > _pagedResult.TotalPages ) )
            return;
        _page = page;
        await LoadCoursesAsync();
    }

    private async Task ChangePageSize( int newSize )
    {
        _pageSize = newSize;
        _page = 1;
        await LoadCoursesAsync();
    }

    private void ViewCourse( CourseListItemDto course )
    {
        // Enrolled courses are always accessible, even after a plan downgrade
        if ( _userTier < course.RequiredTier && !course.IsEnrolled )
        {
            Navigation.NavigateTo( "/pricing" );
            return;
        }
        Navigation.NavigateTo( $"/courses/{course.Id}" );
    }

    private void GoToPricing() => Navigation.NavigateTo( "/pricing" );
}
