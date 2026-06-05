using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

public class RegisterRequestDto
{
    /// <summary>Email address (must be unique).</summary>
    /// <example>new.student@example.com</example>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Password: min 8 chars, at least one uppercase letter and one digit.</summary>
    /// <example>P@ssw0rd123</example>
    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Must match <see cref="Password"/>.</summary>
    /// <example>P@ssw0rd123</example>
    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Given name.</summary>
    /// <example>Maria</example>
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Family name.</summary>
    /// <example>Papadopoulou</example>
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Optional. Use DateTime (client &amp; API). Date-only semantics are stored as DateOnly on the user entity.
    /// </summary>
    /// <example>1998-04-12T00:00:00Z</example>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Must be true to register. GDPR requirement.
    /// </summary>
    /// <example>true</example>
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must consent to data processing.")]
    public bool GdprConsent { get; set; }
}
