using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class Home : IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IBlogConsumer BlogConsumer { get; set; } = default!;
    [Inject] private ITestimonialConsumer TestimonialConsumer { get; set; } = default!;
    [Inject] private PersistentComponentState ApplicationState { get; set; } = default!;

    private IReadOnlyList<BlogArticleSummaryDto>? _blogSummaries;
    private bool _blogLoading = true;

    private IReadOnlyList<TestimonialDto>? _testimonials;
    private bool _testimonialsLoading = true;

    private PersistingComponentStateSubscription _persistSub;

    private string InstagramUrl => Configuration [ "Social:Instagram" ] ?? "https://instagram.com/yourprofile";
    private string YoutubeUrl => Configuration [ "Social:Youtube" ] ?? "https://youtube.com";

    private string? backgroundImageUrl = "/images/background.png";
    private string heroBackgroundStyle => !string.IsNullOrEmpty( backgroundImageUrl )
        ? $"background-image: url('{backgroundImageUrl}'); background-size: cover; background-position: center;"
        : string.Empty;

    private string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        _persistSub = ApplicationState.RegisterOnPersisting( PersistHomeData );

        // Restore from prerender state if available (avoids duplicate API calls on circuit connect)
        var hasTestimonials = ApplicationState.TryTakeFromJson<List<TestimonialDto>>( "home-testimonials" , out var restoredTestimonials );
        var hasBlog = ApplicationState.TryTakeFromJson<List<BlogArticleSummaryDto>>( "home-blog" , out var restoredBlog );

        if ( hasTestimonials )
        {
            _testimonials = restoredTestimonials;
            _testimonialsLoading = false;
        }

        if ( hasBlog )
        {
            _blogSummaries = restoredBlog;
            _blogLoading = false;
        }

        if ( !hasTestimonials || !hasBlog )
        {
            var blogTask = hasBlog ? Task.CompletedTask : LoadBlogAsync();
            var testimonialsTask = hasTestimonials ? Task.CompletedTask : LoadTestimonialsAsync();
            await Task.WhenAll( blogTask , testimonialsTask );
        }
    }

    private Task PersistHomeData()
    {
        ApplicationState.PersistAsJson( "home-testimonials" , _testimonials );
        ApplicationState.PersistAsJson( "home-blog" , _blogSummaries );
        return Task.CompletedTask;
    }

    private async Task LoadBlogAsync()
    {
        try
        {
            _blogSummaries = await BlogConsumer.GetSummariesAsync( count: 6 , lang: CurrentLang );
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

    private static string TestimonialInitials( string fullName )
    {
        if ( string.IsNullOrWhiteSpace( fullName ) ) return "?";
        return string.Concat(
            fullName.Split( ' ' , StringSplitOptions.RemoveEmptyEntries )
                    .Take( 2 )
                    .Select( w => char.ToUpperInvariant( w [ 0 ] ) ) );
    }

    private void NavigateToRegister()
    {
        Navigation.NavigateTo( "/register" );
    }

    public void Dispose() => _persistSub.Dispose();
}
