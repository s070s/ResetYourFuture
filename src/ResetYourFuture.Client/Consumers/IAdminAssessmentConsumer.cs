using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// Client consumer for admin assessment management API operations.
/// </summary>
public interface IAdminAssessmentConsumer
{
    Task<PagedResult<AssessmentDefinitionListItemDto>?> GetAssessmentsAsync( int page = 1 , int pageSize = 10 );
    Task<AdminAssessmentDefinitionDto?> GetAssessmentAsync( Guid id );
    Task<AdminAssessmentDefinitionDto?> CreateAssessmentAsync( SaveAssessmentDefinitionRequest request );
    Task<AdminAssessmentDefinitionDto?> UpdateAssessmentAsync( Guid id , SaveAssessmentDefinitionRequest request );
    Task<bool> DeleteAssessmentAsync( Guid id );
    Task<bool> PublishAssessmentAsync( Guid id );
    Task<bool> UnpublishAssessmentAsync( Guid id );
    Task<PagedResult<AssessmentSubmissionListItemDto>?> GetSubmissionsAsync( Guid id , int page = 1 , int pageSize = 10 );
}
