using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Application.DTOs;

public class AdminSetPasswordDto
{
    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}
