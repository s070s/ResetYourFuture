using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources.Messages;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Pages;

public partial class ForgotPassword
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    // HttpClient retained exclusively for the dev-only reset endpoint (not in IAuthService)
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private ILogger<ForgotPassword> Logger { get; set; } = default!;

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
            var result = await AuthService.ForgotPasswordAsync(forgotPasswordRequest);
            if (result.Success)
            {
                successMessage = result.Message ?? SuccessMessagesRes.PasswordResetLinkSent;
            }
            else
            {
                errorMessage = result.Message ?? ErrorMessagesRes.FailedToSendResetLink;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Forgot-password request failed.");
            errorMessage = ErrorMessagesRes.UnexpectedErrorTryAgain;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task DevResetPassword()
    {
        if (string.IsNullOrEmpty(devNewPassword) || string.IsNullOrEmpty(forgotPasswordRequest.Email))
        {
            errorMessage = ErrorMessagesRes.EnterEmailAndPassword;
            return;
        }

        try
        {
            var request = new
            {
                Email = forgotPasswordRequest.Email,
                NewPassword = devNewPassword
            };
            var response = await Http.PostAsJsonAsync("api/auth/dev/reset-password", request);

            if (response.IsSuccessStatusCode)
            {
                successMessage = SuccessMessagesRes.PasswordResetSuccessful;
            }
            else
            {
                errorMessage = ErrorMessagesRes.PasswordResetFailed;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Dev password reset failed.");
            errorMessage = ErrorMessagesRes.UnexpectedErrorTryAgain;
        }
    }

}
