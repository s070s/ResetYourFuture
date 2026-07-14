using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class Home : IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IBlogConsumer BlogConsumer { get; set; } = default!;
    [Inject] private ITestimonialConsumer TestimonialConsumer { get; set; } = default!;
    [Inject] private ICourseConsumer CourseConsumer { get; set; } = default!;
    [Inject] private PersistentComponentState ApplicationState { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private bool _isAuthenticated;
    private bool _isStudent;
    private string? _authenticatedUserName;
    private bool _authRestored;

    private IReadOnlyList<BlogArticleSummaryDto>? _blogSummaries;
    private bool _blogLoading = true;

    private IReadOnlyList<TestimonialDto>? _testimonials;
    private bool _testimonialsLoading = true;

    // Authenticated "continue learning" shortcuts (UX-7): the student's enrolled courses.
    private IReadOnlyList<CourseListItemDto>? _enrolledCourses;
    private bool _coursesLoading = true;

    private PersistingComponentStateSubscription _persistSub;

    private string? backgroundImageUrl = "/images/background.png";
    private string heroBackgroundStyle => !string.IsNullOrEmpty(backgroundImageUrl)
        ? $"background-image: url('{backgroundImageUrl}'); background-size: cover; background-position: center;"
        : string.Empty;

    private string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    // Restore ALL persisted state BEFORE the first render so the first interactive render
    // exactly matches the SSR pre-rendered HTML — seamless hydration, zero flash.
    // OnInitializedAsync fires after the first render and would introduce a loading-state
    // mismatch if GetAuthenticationStateAsync() ever suspends (intermittent flicker).
    public override async Task SetParametersAsync(ParameterView parameters)
    {
        if (ApplicationState.TryTakeFromJson<bool>("home-isAuthenticated", out var isAuth))
        {
            _isAuthenticated = isAuth;
            ApplicationState.TryTakeFromJson<bool>("home-isStudent", out _isStudent);
            ApplicationState.TryTakeFromJson<string?>("home-userName", out _authenticatedUserName);
            _authRestored = true;
        }
        if (ApplicationState.TryTakeFromJson<List<TestimonialDto>>("home-testimonials", out var t))
        {
            _testimonials = t;
            _testimonialsLoading = false;
        }
        if (ApplicationState.TryTakeFromJson<List<BlogArticleSummaryDto>>("home-blog", out var b))
        {
            _blogSummaries = b;
            _blogLoading = false;
        }
        if (ApplicationState.TryTakeFromJson<List<CourseListItemDto>>("home-courses", out var c))
        {
            _enrolledCourses = c;
            _coursesLoading = false;
        }
        await base.SetParametersAsync(parameters);
    }

    protected override async Task OnInitializedAsync()
    {
        _persistSub = ApplicationState.RegisterOnPersisting(PersistHomeData);

        // Resolve auth state only when not already restored from prerender persistence
        if (!_authRestored)
        {
            var state = await AuthStateProvider.GetAuthenticationStateAsync();
            _isAuthenticated = state.User.Identity?.IsAuthenticated ?? false;
            _isStudent = state.User.IsInRole("Student");
            _authenticatedUserName = state.User.Identity?.Name;
        }

        // Load only what wasn't already restored — flags are false when SetParametersAsync
        // successfully restored the data, so no duplicate API calls occur. The authenticated
        // dashboard needs enrolled courses; the anonymous landing needs blog + testimonials.
        var tasks = new List<Task>();
        if (_isStudent)
        {
            if (_coursesLoading) tasks.Add(LoadEnrolledCoursesAsync());
        }
        else if (!_isAuthenticated)
        {
            if (_blogLoading) tasks.Add(LoadBlogAsync());
            if (_testimonialsLoading) tasks.Add(LoadTestimonialsAsync());
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private Task PersistHomeData()
    {
        ApplicationState.PersistAsJson("home-isAuthenticated", _isAuthenticated);
        ApplicationState.PersistAsJson("home-isStudent", _isStudent);
        ApplicationState.PersistAsJson("home-userName", _authenticatedUserName);
        ApplicationState.PersistAsJson("home-testimonials", _testimonials);
        ApplicationState.PersistAsJson("home-blog", _blogSummaries);
        ApplicationState.PersistAsJson("home-courses", _enrolledCourses);
        return Task.CompletedTask;
    }

    private async Task LoadBlogAsync()
    {
        try
        {
            _blogSummaries = await BlogConsumer.GetSummariesAsync(count: 6, lang: CurrentLang);
        }
        catch
        {
            // Blog section is non-critical — silently skip if unavailable
        }
        finally
        {
            _blogLoading = false;
        }
    }

    private async Task LoadTestimonialsAsync()
    {
        try
        {
            _testimonials = await TestimonialConsumer.GetActiveAsync();
        }
        catch
        {
            // Testimonials section is non-critical — silently skip if unavailable
        }
        finally
        {
            _testimonialsLoading = false;
        }
    }

    private async Task LoadEnrolledCoursesAsync()
    {
        try
        {
            // The catalog list carries an IsEnrolled flag; filter to the user's own courses
            // for the "continue learning" shortcuts. Page size covers any plan's course cap.
            var result = await CourseConsumer.GetCoursesAsync(page: 1, pageSize: 100, lang: CurrentLang);
            _enrolledCourses = result.Items.Where(c => c.IsEnrolled).Take(6).ToList();
        }
        catch
        {
            // Dashboard shortcuts are non-critical — skip silently if the catalog is unavailable.
        }
        finally
        {
            _coursesLoading = false;
        }
    }

    private static string TestimonialInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "?";
        return string.Concat(
            fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(w => char.ToUpperInvariant(w[0])));
    }

    private void NavigateToRegister()
    {
        Navigation.NavigateTo("/register");
    }

    public void Dispose() => _persistSub.Dispose();
}
