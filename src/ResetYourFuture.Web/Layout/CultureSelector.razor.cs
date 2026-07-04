using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace ResetYourFuture.Web.Layout;

public partial class CultureSelector
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>True when the supplied two-letter code matches the active UI culture.</summary>
    private static bool IsActive(string twoLetterCode) =>
        string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            twoLetterCode,
            StringComparison.OrdinalIgnoreCase);

    private void SetCulture(string culture)
    {
        var returnUrl = Uri.EscapeDataString(Navigation.Uri);
        Navigation.NavigateTo($"/culture/set?culture={culture}&returnUrl={returnUrl}", forceLoad: true);
    }
}
