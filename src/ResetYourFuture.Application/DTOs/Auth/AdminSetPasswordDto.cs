using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

public class AdminSetPasswordDto
{
    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}
