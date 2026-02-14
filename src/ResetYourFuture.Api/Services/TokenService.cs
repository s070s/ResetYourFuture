using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Api.Interfaces;

namespace ResetYourFuture.Api.Services;

/// <summary>
/// Generates JWT access tokens and opaque refresh tokens.
/// Access tokens are short-lived (configurable). Refresh tokens are opaque strings stored server-side.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;

    public TokenService(
        IConfiguration config,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService)
    {
        _config = config;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
    }

    public async Task<(string AccessToken, DateTime Expiration)> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "60"));

        var roles = await _userManager.GetRolesAsync(user);
        var tier = await _subscriptionService.GetUserTierAsync(user.Id);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("firstName", user.FirstName),
            new("lastName", user.LastName),
            new("status", ((int)user.Status).ToString()),
            new("isEnabled", user.IsEnabled.ToString().ToLowerInvariant()),
            new("subscriptionTier", ((int)tier).ToString())
        };

        // Add role claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
    }

    /// <summary>
    /// Generates a cryptographically secure opaque refresh token.
    /// Store this server-side associated with the user.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
