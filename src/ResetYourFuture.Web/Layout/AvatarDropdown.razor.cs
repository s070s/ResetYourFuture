using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Interfaces;
using System.Security.Claims;

namespace ResetYourFuture.Web.Layout;

public partial class AvatarDropdown : IDisposable
{
    [Inject] private IProfileConsumer ProfileConsumer { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private bool isOpen;
    private bool _isImpersonating;
    private string? avatarDataUrl;

    protected override void OnInitialized()
    {
        AuthStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        _isImpersonating = IsImpersonating( state.User );
        if ( state.User.Identity?.IsAuthenticated == true )
            await LoadAvatarAsync();
    }

    private async void OnAuthStateChanged( Task<AuthenticationState> task )
    {
        var state = await task;
        _isImpersonating = IsImpersonating( state.User );
        if ( state.User.Identity?.IsAuthenticated == true )
            await LoadAvatarAsync();
        else
            avatarDataUrl = null;

        await InvokeAsync( StateHasChanged );
    }

    /// <summary>
    /// Detects impersonation from the cascaded claims — avoids touching HttpContext
    /// which is unavailable inside a Blazor Server circuit.
    /// </summary>
    private static bool IsImpersonating( ClaimsPrincipal user )
        => user.FindFirst( "impersonatedBy" ) is not null;

    private async Task LoadAvatarAsync()
    {
        try
        {
            var profile = await ProfileConsumer.GetProfileAsync();
            if ( profile is not null && !string.IsNullOrEmpty( profile.AvatarPath ) )
            {
                var avatar = await ProfileConsumer.GetAvatarAsync();
                if ( avatar is not null )
                {
                    avatarDataUrl = $"data:{avatar.Value.ContentType};base64,{Convert.ToBase64String( avatar.Value.Data )}";
                    return;
                }
            }
        }
        catch
        {
            // Not authenticated or profile unavailable — show default icon
        }

        avatarDataUrl = null;
    }

    private void ToggleDropdown() => isOpen = !isOpen;

    private void Close() => isOpen = false;

    private void HandleFocusOut()
    {
        _ = Task.Delay( 200 ).ContinueWith( _ =>
        {
            isOpen = false;
            InvokeAsync( StateHasChanged );
        } );
    }

    private async Task HandleLogout()
    {
        isOpen = false;
        var url = await AuthService.LogoutAsync();
        Navigation.NavigateTo( url , forceLoad: true );
    }

    public void Dispose()
    {
        AuthStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
    }
}
