using Microsoft.AspNetCore.Components;

namespace ResetYourFuture.Client.Pages;

public partial class Home
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    // Use the client-served static image. Leading slash ensures the request goes to the client host.
    // Change the filename if you used .jpg instead of .png.
    private string InstagramUrl => Configuration["Social:Instagram"] ?? "https://instagram.com/yourprofile";
    private string YoutubeUrl => Configuration["Social:Youtube"] ?? "https://youtube.com";

    private string? backgroundImageUrl = "/images/background.png";
    private string heroBackgroundStyle => !string.IsNullOrEmpty(backgroundImageUrl)
        ? $"background-image: url('{backgroundImageUrl}'); background-size: cover; background-position: center;"
        : string.Empty;

    private void NavigateToRegister()
    {
        Navigation.NavigateTo("/register");
    }
}
