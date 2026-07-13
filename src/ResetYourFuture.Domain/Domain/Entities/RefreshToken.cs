using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Domain.Entities;

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

    /// <summary>
    /// The user's <c>SecurityStamp</c> at the moment this token was issued (SEC-1). Checked on
    /// every refresh so a password reset / admin-forced reset invalidates outstanding refresh
    /// tokens immediately, instead of leaving them valid for their full 7-30 day lifetime.
    /// Null on rows issued before this field existed — treated as an automatic mismatch (forces
    /// one re-login), which is the safe default for a security-sensitive migration.
    /// </summary>
    public string? SecurityStampAtIssuance { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
