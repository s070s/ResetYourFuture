using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.Auth;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Optional. Format: yyyy-MM-dd
    /// </summary>
    public string? DateOfBirth { get; set; }

    /// <summary>
    /// Must be true to register. GDPR requirement.
    /// </summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must consent to data processing.")]
    public bool GdprConsent { get; set; }
}
