using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Web.Services;
using System.Security.Claims;

namespace ResetYourFuture.Web.Layout;

public partial class AvatarDropdown : IDisposable
{
    [Inject] private IProfileConsumer ProfileConsumer { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private AvatarChangedNotifier AvatarNotifier { get; set; } = default!;
    [Inject] private ILogger<AvatarDropdown> _logger { get; set; } = default!;

    private bool isOpen;
    private bool _isImpersonating;
    private string? avatarUrl;

    protected override void OnInitialized()
    {
        AuthStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
        AvatarNotifier.AvatarChanged += HandleAvatarChanged;
    }

    private async void HandleAvatarChanged()
    {
        try
        {
            await LoadAvatarAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing avatar after upload.");
        }
    }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        _isImpersonating = IsImpersonating(state.User);
        if (state.User.Identity?.IsAuthenticated == true)
            await LoadAvatarAsync();
    }

    private async void OnAuthStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            var state = await task;
            _isImpersonating = IsImpersonating(state.User);
            if (state.User.Identity?.IsAuthenticated == true)
                await LoadAvatarAsync();
            else
                avatarUrl = null;

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing avatar state.");
        }
    }

    /// <summary>
    /// Detects impersonation from the cascaded claims — avoids touching HttpContext
    /// which is unavailable inside a Blazor Server circuit.
    /// </summary>
    private static bool IsImpersonating(ClaimsPrincipal user)
        => user.FindFirst("impersonatedBy") is not null;

    private async Task LoadAvatarAsync()
    {
        try
        {
            var profile = await ProfileConsumer.GetProfileAsync();
            if (profile is not null && !string.IsNullOrEmpty(profile.AvatarPath))
            {
                // PERF-5: point the <img> at the same-origin avatar endpoint (cookie-authenticated
                // via the MultiAuth scheme) instead of fetching the bytes over loopback and pushing
                // a multi-MB base64 data URL through the circuit. The filename-derived version busts
                // the browser cache when a new avatar is uploaded.
                var version = Uri.EscapeDataString(Path.GetFileName(profile.AvatarPath));
                avatarUrl = $"/api/profile/avatar?v={version}";
                return;
            }
        }
        catch
        {
            // Not authenticated or profile unavailable — show default icon
        }

        avatarUrl = null;
    }

    private void ToggleDropdown() => isOpen = !isOpen;

    private void Close() => isOpen = false;

    private async Task HandleFocusOut()
    {
        await Task.Delay(200);
        isOpen = false;
        StateHasChanged();
    }

    private async Task HandleLogout()
    {
        isOpen = false;
        var url = await AuthService.LogoutAsync();
        Navigation.NavigateTo(url, forceLoad: true);
    }

    public void Dispose()
    {
        AuthStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        AvatarNotifier.AvatarChanged -= HandleAvatarChanged;
    }
}
