namespace ResetYourFuture.Shared.Seed;

/// <summary>
/// DTO for seeding courses from JSON files.
/// </summary>
public sealed class CourseSeedDto
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublished { get; init; }
    public List<ModuleSeedDto> Modules { get; init; } = [];
}

/// <summary>
/// DTO for seeding modules from JSON files.
/// </summary>
public sealed class ModuleSeedDto
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public List<LessonSeedDto> Lessons { get; init; } = [];
}

/// <summary>
/// DTO for seeding lessons from JSON files.
/// </summary>
public sealed class LessonSeedDto
{
    public required string Title { get; init; }
    public string? Content { get; init; }
    public string? PdfPath { get; init; }
    public string? VideoPath { get; init; }
    public int? DurationMinutes { get; init; }
    public int SortOrder { get; init; }
}
