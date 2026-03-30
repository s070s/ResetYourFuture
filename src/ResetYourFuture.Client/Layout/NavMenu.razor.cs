using Microsoft.AspNetCore.Components;

namespace ResetYourFuture.Client.Layout;

public partial class NavMenu
{
    [Parameter] public RenderFragment? TrailingContent { get; set; }

    private bool collapseNavMenu = true;

    private string? NavMenuCssClass => collapseNavMenu ? "collapse" : null;

    private void ToggleNavMenu()
    {
        collapseNavMenu = !collapseNavMenu;
    }
}
