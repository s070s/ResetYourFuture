using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Web.Interfaces;
using System.Security.Claims;

namespace ResetYourFuture.Web.Layout;

public partial class ImpersonationBanner : IDisposable
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private ILogger<ImpersonationBanner> _logger { get; set; } = default!;

    private bool _isImpersonating;
    private string _impersonatedEmail = "";
    private string _impersonatedName = "";

    protected override async Task OnInitializedAsync()
    {
        AuthState.AuthenticationStateChanged += OnAuthStateChangedAsync;
        await RefreshAsync( await AuthState.GetAuthenticationStateAsync() );
    }

    private async void OnAuthStateChangedAsync( Task<AuthenticationState> task )
    {
        try
        {
            await RefreshAsync( await task );
            await InvokeAsync( StateHasChanged );
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex , "Error refreshing impersonation state." );
        }
    }

    private Task RefreshAsync( AuthenticationState state )
    {
        // Read the impersonatedBy claim directly from the cascaded principal —
        // avoids touching HttpContext which is null inside a Blazor Server circuit.
        _isImpersonating = state.User.FindFirst( "impersonatedBy" ) is not null;
        if ( _isImpersonating )
        {
            _impersonatedEmail = state.User.FindFirst( ClaimTypes.Email )?.Value
                                 ?? state.User.FindFirst( "email" )?.Value ?? "";
            var firstName = state.User.FindFirst( "firstName" )?.Value ?? "";
            var lastName = state.User.FindFirst( "lastName" )?.Value ?? "";
            _impersonatedName = $"{firstName} {lastName}".Trim();
        }
        else
        {
            _impersonatedEmail = "";
            _impersonatedName = "";
        }

        return Task.CompletedTask;
    }

    private async Task ExitAsync()
    {
        var url = await AuthService.ExitImpersonationAsync();
        Navigation.NavigateTo( url , forceLoad: true );
    }

    public void Dispose()
    {
        AuthState.AuthenticationStateChanged -= OnAuthStateChangedAsync;
    }
}
