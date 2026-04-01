using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class CourseDetail
{
    [Parameter]
    public Guid CourseId
    {
        get; set;
    }

    [Inject] private ICourseConsumer CourseService { get; set; } = default!;
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private CourseDetailDto? _course;
    private SubscriptionTierEnum _userTier = SubscriptionTierEnum.Free;
    private bool _loading = true;
    private bool _enrolling;
    private string? _error;
    private string? _enrollError;
    private HashSet<Guid> _expandedModules = new();

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        var tierTask = SubscriptionService.GetStatusAsync();
        await Task.WhenAll( LoadCourse(), tierTask );

        var status = tierTask.Result;
        if ( status is not null )
            _userTier = status.Tier;
    }

    private async Task LoadCourse()
    {
        _loading = true;
        _error = null;
        _enrollError = null;
        _expandedModules = new();

        try
        {
            _course = await CourseService.GetCourseAsync( CourseId, CurrentLang );
            if ( _course is null )
            {
                _error = "Course not found.";
            }
            else
            {
                var firstModule = _course.Modules.OrderBy( m => m.SortOrder ).FirstOrDefault();
                if ( firstModule is not null )
                    _expandedModules.Add( firstModule.Id );
            }
        }
        catch ( Exception ex )
        {
            _error = "Failed to load course. Please try again.";
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _loading = false;
        }
    }

    private void ToggleModule( Guid moduleId )
    {
        if ( !_expandedModules.Remove( moduleId ) )
            _expandedModules.Add( moduleId );
    }

    private async Task EnrollInCourse()
    {
        _enrolling = true;
        _enrollError = null;
        try
        {
            var result = await CourseService.EnrollAsync( CourseId );
            if ( result?.Success == true )
            {
                await LoadCourse();
            }
            else
            {
                _enrollError = result?.Message ?? "Failed to enroll. Please try again.";
            }
        }
        catch ( Exception ex )
        {
            _enrollError = "Failed to enroll. Please try again.";
            Console.WriteLine( ex.Message );
        }
        finally
        {
            _enrolling = false;
        }
    }

    private void ViewLesson( Guid lessonId )
    {
        if ( _course?.IsEnrolled == true )
        {
            Navigation.NavigateTo( $"/lessons/{lessonId}" );
        }
    }

    private void GoBack()
    {
        Navigation.NavigateTo( "/courses" );
    }

    private void GoToPricing()
    {
        Navigation.NavigateTo( "/pricing" );
    }
}
