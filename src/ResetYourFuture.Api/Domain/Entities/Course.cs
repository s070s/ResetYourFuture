namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Represents a full learning program (e.g. "Reset Your Future").
/// Independent of UI or pricing. Contains ordered Modules.
/// </summary>
public class Course : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Display title of the course.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Optional description for catalog/marketing.
    /// </summary>
    public string? Description { get; set; }

    // Navigation: one Course has many Modules
    public ICollection<Module> Modules { get; set; } = [];

    // Navigation: one Course has many Enrollments
    public ICollection<Enrollment> Enrollments { get; set; } = [];
}
