using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ResetYourFuture.Client.Layout;

public partial class CultureSelector
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private async Task SetCulture(string culture)
    {
        // Persist to localStorage so Program.cs can read it on reload,
        // then reload the current URL — avoids per-URL browser cache issues
        // that occur when navigating to /?culture=xx with forceLoad.
        await JS.InvokeVoidAsync("eval", $"localStorage.setItem('culture','{culture}');location.reload()");
    }
}
