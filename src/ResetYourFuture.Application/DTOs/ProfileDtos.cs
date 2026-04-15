using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Shared.DTOs;
/// <summary>
/// User profile information.
/// </summary>
public record ProfileDto(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? DisplayName,
    string? AvatarPath,
    DateOnly? DateOfBirth
);

/// <summary>
/// Request to update user profile.
/// </summary>
public record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [MaxLength(100)] string? DisplayName,
    DateOnly? DateOfBirth
);

/// <summary>
/// Request to change password.
/// </summary>
public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(128)] string NewPassword
);
