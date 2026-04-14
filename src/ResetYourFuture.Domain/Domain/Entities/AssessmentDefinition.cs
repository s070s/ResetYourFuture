using ResetYourFuture.Shared.DTOs;


namespace ResetYourFuture.Web.Domain.Entities;

/// <summary>
/// Defines a psychosocial assessment template with its questions and structure.
/// Admins create and publish these; students submit responses via AssessmentSubmission.
/// </summary>
public class AssessmentDefinition : AuditableEntity
{
    public Guid Id
    {
        get; set;
    }

    /// <summary>
    /// Unique identifier key (e.g., "career_clarity_v1").
    /// </summary>
    public required string Key
    {
        get; set;
    }

    /// <summary>
    /// Display title for the assessment (English).
    /// </summary>
    public required string TitleEn
    {
        get; set;
    }

    /// <summary>
    /// Display title for the assessment (Greek). Falls back to English when null.
    /// </summary>
    public string? TitleEl
    {
        get; set;
    }

    /// <summary>
    /// Optional description explaining the assessment purpose (English).
    /// </summary>
    public string? DescriptionEn
    {
        get; set;
    }

    /// <summary>
    /// Optional description explaining the assessment purpose (Greek). Falls back to English when null.
    /// </summary>
    public string? DescriptionEl
    {
        get; set;
    }

    /// <summary>
    /// JSON schema defining sections, questions, question types, and options.
    /// Flexible format to support various assessment structures.
    /// </summary>
    public required string SchemaJson
    {
        get; set;
    }

    /// <summary>
    /// Minimum subscription tier required to access this assessment.
    /// Default is Free (all users can access).
    /// </summary>
    public SubscriptionTierEnum RequiredTier { get; set; } = SubscriptionTierEnum.Free;

    // Navigation
    public ICollection<AssessmentSubmission> Submissions { get; set; } = [];
}
