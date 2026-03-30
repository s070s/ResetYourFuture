using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;

namespace ResetYourFuture.Client.Pages;

public partial class Home
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IBlogConsumer BlogConsumer { get; set; } = default!;

    private IReadOnlyList<BlogArticleSummaryDto>? _blogSummaries;
    private bool _blogLoading = true;

    // Use the client-served static image. Leading slash ensures the request goes to the client host.
    // Change the filename if you used .jpg instead of .png.
    private string InstagramUrl => Configuration["Social:Instagram"] ?? "https://instagram.com/yourprofile";
    private string YoutubeUrl => Configuration["Social:Youtube"] ?? "https://youtube.com";

    private string? backgroundImageUrl = "/images/background.png";
    private string heroBackgroundStyle => !string.IsNullOrEmpty(backgroundImageUrl)
        ? $"background-image: url('{backgroundImageUrl}'); background-size: cover; background-position: center;"
        : string.Empty;

    private string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _blogSummaries = await BlogConsumer.GetSummariesAsync( count: 6, lang: CurrentLang );
        }
        catch
        {
            // Blog section is non-critical — silently skip if the API is unreachable
        }
        finally
        {
            _blogLoading = false;
        }
    }

    private void NavigateToRegister()
    {
        Navigation.NavigateTo("/register");
    }
}
