using System.ComponentModel.DataAnnotations;
using ResetYourFuture.Shared.Resources.Messages;

namespace ResetYourFuture.Application.DTOs;

public class RegisterRequestDto
{
    /// <summary>Email address (must be unique).</summary>
    /// <example>new.student@example.com</example>
    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_EmailRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_EmailInvalid))]
    public string Email { get; set; } = string.Empty;

    /// <summary>Password: min 8 chars, at least one uppercase letter and one digit — must match the
    /// Identity policy configured in AuthenticationSetupExtensions (RequiredLength/RequireDigit/RequireUppercase).</summary>
    /// <example>P@ssw0rd123</example>
    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_PasswordRequired))]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_PasswordComplexity))]
    public string Password { get; set; } = string.Empty;

    /// <summary>Must match <see cref="Password"/>.</summary>
    /// <example>P@ssw0rd123</example>
    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_ConfirmPasswordRequired))]
    [Compare(nameof(Password), ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_PasswordMismatch))]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Given name.</summary>
    /// <example>Maria</example>
    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_FirstNameRequired))]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Family name.</summary>
    /// <example>Papadopoulou</example>
    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_LastNameRequired))]
    [MaxLength(100)]
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
    [Range(typeof(bool), "true", "true", ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_GdprConsentRequired))]
    public bool GdprConsent { get; set; }
}
