using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;

namespace ResetYourFuture.Infrastructure.ApiServices;

/// <summary>
/// Generates JWT access tokens and opaque refresh tokens.
/// Access tokens are short-lived (configurable). Refresh tokens are opaque strings stored server-side.
/// </summary>
public class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly double _accessTokenExpirationMinutes;
    private readonly string? _jwtIssuer;
    private readonly string? _jwtAudience;
    private static readonly JwtSecurityTokenHandler _tokenHandler = new() { SetDefaultTimesOnTokenCreation = false };

    public TokenService(
        IConfiguration config,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService)
    {
        _userManager = userManager;
        _subscriptionService = subscriptionService;
        var jwtKey = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException(
                "Jwt:Key is required. Set it via User Secrets (dev) or environment variable Jwt__Key (prod).");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        _accessTokenExpirationMinutes = config.GetValue<double>("Jwt:AccessTokenExpirationMinutes", 60);
        _jwtIssuer = config["Jwt:Issuer"];
        _jwtAudience = config["Jwt:Audience"];
    }

    public async Task<(string AccessToken, DateTime Expiration)> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

        var roles = await _userManager.GetRolesAsync(user);
        var tier = await _subscriptionService.GetUserTierAsync(user.Id);

        var claims = UserClaimsBuilder.Build(user, roles, tier);
        claims.Add(new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: expiration,
            signingCredentials: creds);

        return (_tokenHandler.WriteToken(token), expiration);
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

    public async Task<(string AccessToken, DateTime Expiration)> GenerateImpersonationTokenAsync(ApplicationUser user, string adminId)
    {
        var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

        var roles = await _userManager.GetRolesAsync(user);
        var tier = await _subscriptionService.GetUserTierAsync(user.Id);

        var claims = UserClaimsBuilder.Build(user, roles, tier, adminBackupId: adminId);
        claims.Add(new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: expiration,
            signingCredentials: creds);

        return (_tokenHandler.WriteToken(token), expiration);
    }
}
