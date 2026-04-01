using Microsoft.AspNetCore.Components;

namespace ResetYourFuture.Web.Layout;

public partial class CultureSelector
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private void SetCulture( string culture )
    {
        var returnUrl = Uri.EscapeDataString( Navigation.Uri );
        Navigation.NavigateTo( $"/culture/set?culture={culture}&returnUrl={returnUrl}" , forceLoad: true );
    }
}
