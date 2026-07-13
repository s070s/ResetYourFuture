using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
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
    [Inject] private ILogger<CourseDetail> _logger { get; set; } = default!;

    private CourseDetailDto? _course;
    private SubscriptionTier _userTier = SubscriptionTier.Free;
    private bool _loading = true;
    private bool _enrolling;
    private string? _error;
    private string? _enrollError;
    private HashSet<Guid> _expandedModules = new();

    private CourseReviewsResponseDto? _reviewsData;
    private bool _showReviewForm;
    private bool _submittingReview;
    private int _formRating = 5;
    private string _formBody = string.Empty;
    private string? _reviewError;

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        var tierTask = SubscriptionService.GetStatusAsync();
        await Task.WhenAll(LoadCourse(), LoadReviewsAsync(), tierTask);

        var status = await tierTask;
        if (status is not null)
            _userTier = status.Tier;
    }

    private async Task LoadReviewsAsync()
    {
        try
        {
            _reviewsData = await CourseService.GetReviewsAsync(CourseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load reviews for course {CourseId}.", CourseId);
        }
    }

    private void OpenReviewForm()
    {
        _formRating = _reviewsData?.MyReview?.Rating ?? 5;
        _formBody = _reviewsData?.MyReview?.Body ?? string.Empty;
        _reviewError = null;
        _showReviewForm = true;
    }

    private void CloseReviewForm() => _showReviewForm = false;

    private async Task SubmitReviewAsync()
    {
        if (string.IsNullOrWhiteSpace(_formBody))
            return;

        _submittingReview = true;
        _reviewError = null;
        try
        {
            var result = await CourseService.SaveReviewAsync(CourseId, new SaveCourseReviewRequest(_formRating, _formBody.Trim()));
            if (result is not null)
            {
                _showReviewForm = false;
                await LoadReviewsAsync();
            }
            else
            {
                _reviewError = ReviewRes.SubmitError;
            }
        }
        catch (Exception ex)
        {
            _reviewError = ReviewRes.SubmitError;
            _logger.LogError(ex, "Failed to submit review for course {CourseId}.", CourseId);
        }
        finally
        {
            _submittingReview = false;
        }
    }

    private async Task LoadCourse()
    {
        _loading = true;
        _error = null;
        _enrollError = null;
        _expandedModules = new();

        try
        {
            _course = await CourseService.GetCourseAsync(CourseId, CurrentLang);
            if (_course is null)
            {
                _error = ErrorMessagesRes.CourseNotFound;
            }
            else
            {
                var firstModule = _course.Modules.OrderBy(m => m.SortOrder).FirstOrDefault();
                if (firstModule is not null)
                    _expandedModules.Add(firstModule.Id);
            }
        }
        catch (Exception ex)
        {
            _error = ErrorMessagesRes.FailedToLoadCourse;
            _logger.LogError(ex, "Failed to load course {CourseId}.", CourseId);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ToggleModule(Guid moduleId)
    {
        if (!_expandedModules.Remove(moduleId))
            _expandedModules.Add(moduleId);
    }

    private async Task EnrollInCourse()
    {
        _enrolling = true;
        _enrollError = null;
        try
        {
            var result = await CourseService.EnrollAsync(CourseId);
            if (result?.Success == true)
            {
                await LoadCourse();
            }
            else
            {
                _enrollError = result?.Message ?? "Failed to enroll. Please try again.";
            }
        }
        catch (Exception ex)
        {
            _enrollError = ErrorMessagesRes.FailedToEnroll;
            _logger.LogError(ex, "Failed to enroll in course {CourseId}.", CourseId);
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

    private void GoToPricing()
    {
        Navigation.NavigateTo("/pricing");
    }
}
