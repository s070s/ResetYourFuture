using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Interfaces;

/// <summary>
/// Token generation service for JWT access tokens and refresh tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    /// <param name="user">The application user</param>
    /// <returns>Access token and expiration timestamp</returns>
    Task<(string AccessToken, DateTime Expiration)> GenerateAccessTokenAsync(ApplicationUser user);

    /// <summary>
    /// Generates a cryptographically secure opaque refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Generates a short-lived JWT access token for impersonation.
    /// The token carries an extra <c>impersonatedBy</c> claim with the admin's user ID.
    /// </summary>
    Task<(string AccessToken, DateTime Expiration)> GenerateImpersonationTokenAsync(ApplicationUser user, string adminId);
}
