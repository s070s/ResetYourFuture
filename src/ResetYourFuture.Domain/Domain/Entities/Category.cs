namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// A classification tag shared by Courses and AssessmentDefinitions.
/// Always visible to admins/students; IsPublished (from AuditableEntity) is not used.
/// </summary>
public class Category : AuditableEntity
{
    public Guid Id
    {
        get; set;
    }

    /// <summary>
    /// Display name (English).
    /// </summary>
    public required string NameEn
    {
        get; set;
    }

    /// <summary>
    /// Display name (Greek). Falls back to English when null.
    /// </summary>
    public string? NameEl
    {
        get; set;
    }

    // Navigation: one Category has many Courses
    public ICollection<Course> Courses { get; set; } = [];

    // Navigation: one Category has many AssessmentDefinitions
    public ICollection<AssessmentDefinition> AssessmentDefinitions { get; set; } = [];
}
