using Microsoft.AspNetCore.Components;

namespace ResetYourFuture.Client.Layout;

public partial class CultureSelector
{
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private void SetCulture(string culture)
    {
        //Refactor this to use a dedicated endpoint
        // Always route to home page after language change.
        // Navigate to the client root with ?culture=... and force a full reload so Program.cs reads the query on startup.
        var redirectUri = "/";
        var target = $"{redirectUri}?culture={culture}";
        Nav.NavigateTo(target, forceLoad: true);
    }
}
