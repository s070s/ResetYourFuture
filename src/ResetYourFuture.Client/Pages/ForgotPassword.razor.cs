using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class ForgotPassword
{
    [Inject] private HttpClient Http { get; set; } = default!;

    private ForgotPasswordRequest forgotPasswordRequest = new();
    private string? successMessage;
    private string? errorMessage;
    private bool isLoading;
    private string devNewPassword = string.Empty;

    private async Task HandleSubmit()
    {
        isLoading = true;
        successMessage = null;
        errorMessage = null;

        forgotPasswordRequest.Email = forgotPasswordRequest.Email?.Trim() ?? string.Empty;

        try
        {
            var response = await Http.PostAsJsonAsync( "api/auth/forgot-password" , forgotPasswordRequest );
            if ( response.IsSuccessStatusCode )
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                successMessage = result?.Message ?? "If the email exists, a reset link has been sent.";
            }
            else
            {
                errorMessage = "Error sending reset link";
            }
        }
        catch ( Exception ex )
        {
            errorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task DevResetPassword()
    {
        if ( string.IsNullOrEmpty( devNewPassword ) || string.IsNullOrEmpty( forgotPasswordRequest.Email ) )
        {
            errorMessage = "Please enter email and new password";
            return;
        }

        try
        {
            var request = new
            {
                Email = forgotPasswordRequest.Email ,
                NewPassword = devNewPassword
            };
            var response = await Http.PostAsJsonAsync( "api/auth/dev/reset-password" , request );

            if ( response.IsSuccessStatusCode )
            {
                successMessage = "Password reset successfully! You can now log in with the new password.";
            }
            else
            {
                errorMessage = "Error resetting password";
            }
        }
        catch ( Exception ex )
        {
            errorMessage = $"Error: {ex.Message}";
        }
    }

    private class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    private class AuthResponse
    {
        public bool Success
        {
            get; set;
        }
        public string? Message
        {
            get; set;
        }
    }
}
