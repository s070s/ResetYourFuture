namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// DTO for seeding assessment definitions from JSON files.
/// </summary>
public sealed class AssessmentSeedDto
{
    public required string Key
    {
        get; init;
    }
    public required string Title
    {
        get; init;
    }
    public string? Description
    {
        get; init;
    }
    public required string SchemaJson
    {
        get; init;
    }
    public bool IsPublished
    {
        get; init;
    }
    public DateTimeOffset? CreatedAt
    {
        get; init;
    }
    public DateTimeOffset? PublishedAt
    {
        get; init;
    }
}