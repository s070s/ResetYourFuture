namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// DTO for seeding student users from JSON files.
/// </summary>
public sealed class StudentSeedDto
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; init; }
}
