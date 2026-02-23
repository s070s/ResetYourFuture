using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.Auth;
using ResetYourFuture.Shared.Resources.Messages;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class Login
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IWebAssemblyHostEnvironment Env { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private LoginRequest loginRequest = new();
    private string? errorMessage;
    private string? devSuccessMessage;
    private bool isLoading;
    private bool showPassword;

    // Tracks whether the most recent failure is due to unconfirmed email and which email is pending
    private bool unconfirmedEmailPending;
    private string pendingUnconfirmedEmail = string.Empty;

    private string passwordInputType => showPassword ? "text" : "password";
    private string passwordIconClass => showPassword ? "fa-solid fa-eye-slash" : "fa-solid fa-eye";

    private void TogglePasswordVisibility()
    {
        showPassword = !showPassword;
    }

    private async Task HandleLogin()
    {
        isLoading = true;
        errorMessage = null;
        devSuccessMessage = null;
        unconfirmedEmailPending = false;
        pendingUnconfirmedEmail = string.Empty;

        // Trim inputs
        loginRequest.Email = loginRequest.Email?.Trim() ?? string.Empty;
        loginRequest.Password = loginRequest.Password?.Trim() ?? string.Empty;

        var result = await AuthService.LoginAsync( loginRequest );

        if ( result.Success )
        {
            Navigation.NavigateTo( "/" );
        }
        else
        {
            // Preserve original message
            errorMessage = result.Message ?? ErrorMessagesRes.LoginError;

            // Detect "email not confirmed" failure and surface a dev-only confirm action.
            // NOTE: without a machine-readable failure code from the API, the client must infer this.
            // If you want a robust non-string check, add a structured flag to the API response.
            if ( Env.IsDevelopment() && !string.IsNullOrEmpty( result.Message )
                && result.Message.Contains( "email not confirmed" , StringComparison.OrdinalIgnoreCase ) )
            {
                unconfirmedEmailPending = true;
                pendingUnconfirmedEmail = loginRequest.Email;
                // Hide generic error when showing the targeted unconfirmed-email UI
                errorMessage = null;
            }
        }

        isLoading = false;
    }

    private async Task DevConfirmPendingEmail()
    {
        if ( string.IsNullOrEmpty( pendingUnconfirmedEmail ) )
            return;

        try
        {
            // Call existing dev-only API used on Register page. This endpoint is development-only and guarded server-side.
            var response = await Http.PostAsJsonAsync( "api/auth/dev/confirm-email" , pendingUnconfirmedEmail );

            if ( response.IsSuccessStatusCode )
            {
                unconfirmedEmailPending = false;
                devSuccessMessage = SuccessMessagesRes.EmailConfirmationSuccess;
            }
            else
            {
                // Try to read returned error message if available
                var apiResp = await response.Content.ReadFromJsonAsync<AuthResponse>();
                errorMessage = apiResp?.Message ?? ErrorMessagesRes.EmailConfirmationError;
            }
        }
        catch ( Exception ex )
        {
            errorMessage = $"{ErrorMessagesRes.EmailConfirmationError}: {ex.Message}";
        }
    }
}
