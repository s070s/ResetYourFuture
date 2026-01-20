using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Defines a psychosocial assessment template with its questions and structure.
/// Admins create and publish these; students submit responses via AssessmentSubmission.
/// </summary>
public class AssessmentDefinition : AuditableEntity
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Unique identifier key (e.g., "career_clarity_v1").
    /// </summary>
    public required string Key { get; set; }
    
    /// <summary>
    /// Display title for the assessment.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Optional description explaining the assessment purpose.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// JSON schema defining sections, questions, question types, and options.
    /// Flexible format to support various assessment structures.
    /// </summary>
    public required string SchemaJson { get; set; }
    
    // Navigation
    public ICollection<AssessmentSubmission> Submissions { get; set; } = [];
}
