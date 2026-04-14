using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Interfaces;

/// <summary>
/// Authentication service interface for SSR auth operations.
///
/// Sign-in / sign-out methods return redirect URLs instead of performing cookie
/// operations directly, because those operations require a fresh HTTP request
/// (Blazor Server circuits run after the response has already been committed).
///
/// Callers must call NavigationManager.NavigateTo(url, forceLoad: true) with the
/// returned URL.  The /auth/complete and /auth/signout minimal endpoints handle
/// the actual cookie writing.
/// </summary>
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync( RegisterRequestDto request );

    /// <summary>
    /// Validates credentials.  On success, <see cref="AuthResponseDto.Token"/>
    /// holds a short-lived ticket GUID.  Navigate to
    /// <c>/auth/complete?ticket={Token}&amp;returnUrl=…</c> with forceLoad: true.
    /// </summary>
    Task<AuthResponseDto> LoginAsync( LoginRequestDto request );

    /// <summary>Returns the URL to navigate to (forceLoad: true) to sign out.</summary>
    Task<string> LogoutAsync();

    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Generates a JWT from HttpContext.User claims. Only valid during HTTP requests (SSR/API).
    /// Use <see cref="GetTokenAsync(ClaimsPrincipal)"/> inside Blazor Server circuits.
    /// </summary>
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Generates a JWT from the supplied principal. Safe to call inside Blazor Server circuits
    /// where HttpContext is unavailable — pass the cascaded AuthenticationState.User.
    /// </summary>
    Task<string?> GetTokenAsync( ClaimsPrincipal principal );

    /// <summary>
    /// Validates the impersonation target.  On success, <see cref="AuthResponseDto.Token"/>
    /// holds a short-lived ticket GUID.  Navigate to
    /// <c>/auth/complete?ticket={Token}&amp;returnUrl=…</c> with forceLoad: true.
    /// </summary>
    Task<AuthResponseDto> ImpersonateAsync( string userId );

    /// <summary>Returns the URL to navigate to (forceLoad: true) to restore the admin session.</summary>
    Task<string> ExitImpersonationAsync();

    Task<bool> IsImpersonatingAsync();
    Task<AuthResponseDto> ForgotPasswordAsync( ForgotPasswordRequestDto request );
    Task<AuthResponseDto> ResetPasswordAsync( ResetPasswordRequestDto request );
}
