using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Client.Interfaces;

namespace ResetYourFuture.Client.Layout;

public partial class ImpersonationBanner : IDisposable
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;

    private bool _isImpersonating;
    private string _impersonatedEmail = "";
    private string _impersonatedName = "";

    protected override async Task OnInitializedAsync()
    {
        AuthState.AuthenticationStateChanged += OnAuthStateChangedAsync;
        await RefreshAsync(await AuthState.GetAuthenticationStateAsync());
    }

    private async void OnAuthStateChangedAsync(Task<AuthenticationState> task)
    {
        await RefreshAsync(await task);
        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshAsync(AuthenticationState state)
    {
        _isImpersonating = await AuthService.IsImpersonatingAsync();
        if (_isImpersonating)
        {
            _impersonatedEmail = state.User.FindFirst("email")?.Value ?? "";
            var firstName = state.User.FindFirst("firstName")?.Value ?? "";
            var lastName = state.User.FindFirst("lastName")?.Value ?? "";
            _impersonatedName = $"{firstName} {lastName}".Trim();
        }
    }

    private async Task ExitAsync()
    {
        await AuthService.ExitImpersonationAsync();
        Navigation.NavigateTo("/admin/users", forceLoad: false);
    }

    public void Dispose()
    {
        AuthState.AuthenticationStateChanged -= OnAuthStateChangedAsync;
    }
}
