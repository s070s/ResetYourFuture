using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Application.DTOs;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
