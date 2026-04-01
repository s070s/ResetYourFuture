using Microsoft.AspNetCore.Identity;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.Identity;

/// <summary>
/// Custom user entity. Extend here instead of using raw IdentityUser.
/// </summary>
public class ApplicationUser : IdentityUser
{
    // --- Profile ---
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    
    public string? DisplayName { get; set; }
    
    public string? AvatarPath { get; set; }

    /// <summary>
    /// Stored as DATE in SQL Server. Use DateOnly for clean semantics.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Computed age (not persisted). Returns null if DateOfBirth is not set.
    /// </summary>
    public int? Age
    {
        get
        {
            if (DateOfBirth is null) return null;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - DateOfBirth.Value.Year;
            if (DateOfBirth.Value > today.AddYears(-age)) age--;
            return age;
        }
    }

    /// <summary>
    /// Extensible status stored as int for forward compatibility.
    /// </summary>
    public UserStatus Status { get; set; } = UserStatus.Unknown;

    /// <summary>
    /// When false the user is locked out of the platform.
    /// Admin can toggle via /admin/users.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    // --- GDPR / Compliance ---
    /// <summary>
    /// Explicit consent to data processing. Must be true for registration to complete.
    /// </summary>
    public bool GdprConsentGiven { get; set; }

    public DateTime? GdprConsentDate { get; set; }

    /// <summary>
    /// Placeholder: if user is under 18, parental consent may be required.
    /// Implementation deferred; this flag allows future gating.
    /// </summary>
    public bool ParentalConsentGiven { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // --- Navigation Properties ---
    
    /// <summary>
    /// Courses the user is enrolled in.
    /// </summary>
    public ICollection<Enrollment> Enrollments { get; set; } = [];

    /// <summary>
    /// Assessment submissions by the user.
    /// </summary>
    public ICollection<AssessmentSubmission> AssessmentSubmissions { get; set; } = [];

    /// <summary>
    /// Subscription history. Only one should be active at a time.
    /// </summary>
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = [];
    
    /// <summary>
    /// Refresh tokens for this user.
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
