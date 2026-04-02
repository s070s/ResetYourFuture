using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin assessment management API.
/// </summary>
public class AdminAssessmentConsumer( HttpClient http ) : ApiClientBase( http ), IAdminAssessmentConsumer
{
    public Task<PagedResult<AssessmentDefinitionListItemDto>?> GetAssessmentsAsync( int page = 1, int pageSize = 10 )
        => GetAsync<PagedResult<AssessmentDefinitionListItemDto>>( $"api/admin/assessments?page={page}&pageSize={pageSize}" );

    public Task<AdminAssessmentDefinitionDto?> GetAssessmentAsync( Guid id )
        => GetAsync<AdminAssessmentDefinitionDto>( $"api/admin/assessments/{id}" );

    public Task<AdminAssessmentDefinitionDto?> CreateAssessmentAsync( SaveAssessmentDefinitionRequest request )
        => PostJsonAsync<SaveAssessmentDefinitionRequest, AdminAssessmentDefinitionDto>( "api/admin/assessments", request );

    public Task<AdminAssessmentDefinitionDto?> UpdateAssessmentAsync( Guid id, SaveAssessmentDefinitionRequest request )
        => PutJsonAsync<SaveAssessmentDefinitionRequest, AdminAssessmentDefinitionDto>( $"api/admin/assessments/{id}", request );

    public Task<bool> DeleteAssessmentAsync( Guid id )
        => DeleteAsync( $"api/admin/assessments/{id}" );

    public Task<bool> PublishAssessmentAsync( Guid id )
        => ActionAsync( $"api/admin/assessments/{id}/publish" );

    public Task<bool> UnpublishAssessmentAsync( Guid id )
        => ActionAsync( $"api/admin/assessments/{id}/unpublish" );

    public Task<PagedResult<AssessmentSubmissionListItemDto>?> GetSubmissionsAsync( Guid id, int page = 1, int pageSize = 10 )
        => GetAsync<PagedResult<AssessmentSubmissionListItemDto>>( $"api/admin/assessments/{id}/submissions?page={page}&pageSize={pageSize}" );
}
