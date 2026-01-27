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
    /// <returns>Base64-encoded refresh token</returns>
    string GenerateRefreshToken();
}
