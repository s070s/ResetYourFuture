using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

public class RegisterRequestDto
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
    /// Optional. Use DateTime (client & API). Date-only semantics are stored as DateOnly on the user entity.
    /// </summary>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Must be true to register. GDPR requirement.
    /// </summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must consent to data processing.")]
    public bool GdprConsent { get; set; }
}
