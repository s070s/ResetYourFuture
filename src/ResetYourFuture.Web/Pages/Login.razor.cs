using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Shared.Resources.Messages;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Pages;

public partial class Login
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IWebHostEnvironment Env { get; set; } = default!;
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    private LoginRequestDto loginRequest = new();
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

        try
        {
            var result = await AuthService.LoginAsync( loginRequest );

            if ( result.Success )
            {
                // Navigate forceLoad so the fresh HTTP request can set the auth cookie.
                Navigation.NavigateTo(
                    $"/auth/complete?ticket={Uri.EscapeDataString( result.Token! )}&returnUrl=%2F" ,
                    forceLoad: true );
                return; // navigation is underway — don't touch component state
            }
            else
            {
                // Preserve original message
                errorMessage = result.Message ?? ErrorMessagesRes.LoginError;

                // Detect "email not confirmed" failure and surface a dev-only confirm action.
                if ( Env.IsDevelopment() && !string.IsNullOrEmpty( result.Message )
                    && result.Message.Contains( "email not confirmed" , StringComparison.OrdinalIgnoreCase ) )
                {
                    unconfirmedEmailPending = true;
                    pendingUnconfirmedEmail = loginRequest.Email;
                    // Hide generic error when showing the targeted unconfirmed-email UI
                    errorMessage = null;
                }
            }
        }
        catch ( HttpRequestException )
        {
            errorMessage = "Unable to connect to the server. Please try again.";
        }
        catch ( Exception ex )
        {
            errorMessage = $"An unexpected error occurred: {ex.Message}";
        }

        isLoading = false;
    }

    private async Task DevConfirmPendingEmail()
    {
        if ( string.IsNullOrEmpty( pendingUnconfirmedEmail ) )
            return;

        try
        {
            var http = HttpClientFactory.CreateClient( "SelfClient" );
            var response = await http.PostAsJsonAsync( "api/auth/dev/confirm-email" , pendingUnconfirmedEmail );

            if ( response.IsSuccessStatusCode )
            {
                unconfirmedEmailPending = false;
                devSuccessMessage = SuccessMessagesRes.EmailConfirmationSuccess;
            }
            else
            {
                var apiResp = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                errorMessage = apiResp?.Message ?? ErrorMessagesRes.EmailConfirmationError;
            }
        }
        catch ( Exception ex )
        {
            errorMessage = $"{ErrorMessagesRes.EmailConfirmationError}: {ex.Message}";
        }
    }
}
