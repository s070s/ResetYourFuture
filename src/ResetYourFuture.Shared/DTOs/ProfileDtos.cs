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
    string FirstName,
    string LastName,
    string? DisplayName,
    DateOnly? DateOfBirth
);

/// <summary>
/// Request to change password.
/// </summary>
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);
