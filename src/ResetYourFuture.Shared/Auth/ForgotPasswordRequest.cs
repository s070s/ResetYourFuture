using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.Auth;

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
