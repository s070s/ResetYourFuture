using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
