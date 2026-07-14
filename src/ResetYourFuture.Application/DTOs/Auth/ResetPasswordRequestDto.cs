using System.ComponentModel.DataAnnotations;
using ResetYourFuture.Shared.Resources.Messages;

namespace ResetYourFuture.Application.DTOs;

public class ResetPasswordRequestDto
{
    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_EmailRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_EmailInvalid))]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>Min 8 chars, at least one uppercase letter and one digit — matches the Identity
    /// policy configured in AuthenticationSetupExtensions.</summary>
    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_PasswordRequired))]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_PasswordComplexity))]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_ConfirmPasswordRequired))]
    [Compare(nameof(NewPassword), ErrorMessageResourceType = typeof(ErrorMessagesRes), ErrorMessageResourceName = nameof(ErrorMessagesRes.Auth_PasswordMismatch))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
