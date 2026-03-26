using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Represents a full learning program (e.g. "Reset Your Future").
/// Independent of UI or pricing. Contains ordered Modules.
/// </summary>
public class Course : AuditableEntity
{
    public Guid Id
    {
        get; set;
    }

    /// <summary>
    /// Display title of the course (English).
    /// </summary>
    public required string TitleEn
    {
        get; set;
    }

    /// <summary>
    /// Display title of the course (Greek). Falls back to English when null.
    /// </summary>
    public string? TitleEl
    {
        get; set;
    }

    /// <summary>
    /// Optional description for catalog/marketing (English).
    /// </summary>
    public string? DescriptionEn
    {
        get; set;
    }

    /// <summary>
    /// Optional description for catalog/marketing (Greek). Falls back to English when null.
    /// </summary>
    public string? DescriptionEl
    {
        get; set;
    }

    /// <summary>
    /// Minimum subscription tier required to access this course.
    /// Default is Free (all users can access).
    /// </summary>
    public SubscriptionTierEnum RequiredTier { get; set; } = SubscriptionTierEnum.Free;

    // Navigation: one Course has many Modules
    public ICollection<Module> Modules { get; set; } = [];

    // Navigation: one Course has many Enrollments
    public ICollection<Enrollment> Enrollments { get; set; } = [];
}
