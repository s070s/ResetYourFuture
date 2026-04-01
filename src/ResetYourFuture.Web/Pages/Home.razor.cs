using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private bool _isAuthenticated;
    private string? _authenticatedUserName;
    private bool _authRestored;

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

    // Restore ALL persisted state BEFORE the first render so the first interactive render
    // exactly matches the SSR pre-rendered HTML — seamless hydration, zero flash.
    // OnInitializedAsync fires after the first render and would introduce a loading-state
    // mismatch if GetAuthenticationStateAsync() ever suspends (intermittent flicker).
    public override async Task SetParametersAsync( ParameterView parameters )
    {
        if ( ApplicationState.TryTakeFromJson<bool>( "home-isAuthenticated" , out var isAuth ) )
        {
            _isAuthenticated = isAuth;
            ApplicationState.TryTakeFromJson<string?>( "home-userName" , out _authenticatedUserName );
            _authRestored = true;
        }
        if ( ApplicationState.TryTakeFromJson<List<TestimonialDto>>( "home-testimonials" , out var t ) )
        {
            _testimonials = t;
            _testimonialsLoading = false;
        }
        if ( ApplicationState.TryTakeFromJson<List<BlogArticleSummaryDto>>( "home-blog" , out var b ) )
        {
            _blogSummaries = b;
            _blogLoading = false;
        }
        await base.SetParametersAsync( parameters );
    }

    protected override async Task OnInitializedAsync()
    {
        _persistSub = ApplicationState.RegisterOnPersisting( PersistHomeData );

        // Resolve auth state only when not already restored from prerender persistence
        if ( !_authRestored )
        {
            var state = await AuthStateProvider.GetAuthenticationStateAsync();
            _isAuthenticated = state.User.Identity?.IsAuthenticated ?? false;
            _authenticatedUserName = state.User.Identity?.Name;
        }

        // Load only what wasn't already restored — flags are false when SetParametersAsync
        // successfully restored the data, so no duplicate API calls occur.
        var tasks = new List<Task>();
        if ( _blogLoading ) tasks.Add( LoadBlogAsync() );
        if ( _testimonialsLoading ) tasks.Add( LoadTestimonialsAsync() );
        if ( tasks.Count > 0 )
            await Task.WhenAll( tasks );
    }

    private Task PersistHomeData()
    {
        ApplicationState.PersistAsJson( "home-isAuthenticated" , _isAuthenticated );
        ApplicationState.PersistAsJson( "home-userName" , _authenticatedUserName );
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
