using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

public class ForgotPasswordRequestDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
