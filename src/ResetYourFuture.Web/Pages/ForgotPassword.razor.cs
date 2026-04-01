using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Pages;

public partial class ForgotPassword
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    // HttpClient retained exclusively for the dev-only reset endpoint (not in IAuthService)
    [Inject] private HttpClient Http { get; set; } = default!;

    private ForgotPasswordRequestDto forgotPasswordRequest = new();
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
            var result = await AuthService.ForgotPasswordAsync( forgotPasswordRequest );
            if ( result.Success )
            {
                successMessage = result.Message ?? "If the email exists, a reset link has been sent.";
            }
            else
            {
                errorMessage = result.Message ?? "Error sending reset link";
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

}
