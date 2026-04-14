using ResetYourFuture.Web.Identity;

namespace ResetYourFuture.Web.Domain.Entities;

/// <summary>
/// Stores refresh tokens for JWT authentication.
/// Supports token rotation and revocation.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    
    public required string UserId { get; set; }
    
    /// <summary>
    /// Hashed refresh token (never store plain tokens).
    /// </summary>
    public required string TokenHash { get; set; }
    
    public DateTimeOffset ExpiresAt { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? RevokedAt { get; set; }
    
    /// <summary>
    /// For token rotation: points to the new token that replaced this one.
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }
    
    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
