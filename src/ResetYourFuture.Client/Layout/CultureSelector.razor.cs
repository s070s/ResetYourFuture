using Microsoft.AspNetCore.Components;

namespace ResetYourFuture.Client.Layout;

public partial class CultureSelector
{
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private void SetCulture(string culture)
    {
        var currentUri = new Uri(Nav.Uri);

        // Preserve existing query params except any stale 'culture' entry
        var existingParts = currentUri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("culture=", StringComparison.OrdinalIgnoreCase))
            .ToList();

        existingParts.Add($"culture={culture}");

        var target = currentUri.AbsolutePath + "?" + string.Join("&", existingParts);
        Nav.NavigateTo(target, forceLoad: true);
    }
}
