using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// Client consumer for user-facing assessment API operations.
/// </summary>
public interface IAssessmentConsumer
{
    Task<PagedResult<AssessmentDefinitionDto>> GetAssessmentsAsync( int page = 1, int pageSize = 10, string lang = "en" );
    Task<AssessmentDefinitionDto?> GetAssessmentAsync( Guid id, string lang = "en" );
    Task<AssessmentSubmissionDto?> SubmitAssessmentAsync( Guid id, SubmitAssessmentRequest request );
    Task<List<AssessmentSubmissionDto>> GetMySubmissionsAsync();
}
