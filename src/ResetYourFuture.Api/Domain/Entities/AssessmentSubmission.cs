using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// Student's response to an AssessmentDefinition.
/// Stores answers and optional summary (no scoring - descriptive only).
/// </summary>
public class AssessmentSubmission
{
    public Guid Id { get; set; }
    
    public Guid AssessmentDefinitionId { get; set; }
    
    public required string UserId { get; set; }
    
    /// <summary>
    /// JSON containing the user's answers to the assessment questions.
    /// </summary>
    public required string AnswersJson { get; set; }
    
    /// <summary>
    /// Optional JSON summary for descriptive insights (not scoring).
    /// </summary>
    public string? SummaryJson { get; set; }
    
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation
    public AssessmentDefinition AssessmentDefinition { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
