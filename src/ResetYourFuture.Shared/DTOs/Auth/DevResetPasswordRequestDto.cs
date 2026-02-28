using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

public class DevResetPasswordRequestDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}
