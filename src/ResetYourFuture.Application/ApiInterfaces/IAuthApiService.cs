using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// JSON API authentication: register/login/refresh with JWT access + refresh tokens.
/// Distinct from <see cref="IAuthService"/>, which backs the Blazor Server cookie-based
/// SSR sign-in flow and returns redirect URLs instead of tokens.
/// </summary>
public interface IAuthApiService
{
    /// <param name="request">Registration details.</param>
    /// <param name="buildConfirmUrl">Builds the email-confirmation URL from (userId, token).</param>
    Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, Func<string, string, string?> buildConfirmUrl);
    Task<ServiceResult<AuthResponseDto>> ConfirmEmailAsync(string userId, string token);
    Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request);

    /// <param name="request">The email to send a reset link to.</param>
    /// <param name="buildResetUrl">Builds the password-reset URL from (email, token).</param>
    Task<AuthResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request, Func<string, string, string> buildResetUrl);
    Task<ServiceResult<AuthResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request);
    Task<CurrentUserDto?> GetCurrentUserAsync(string userId);

    Task<ServiceResult<AuthResponseDto>> DevConfirmEmailAsync(string email);
    Task<ServiceResult<AuthResponseDto>> DevResetPasswordAsync(DevResetPasswordRequestDto request);
}
