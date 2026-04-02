using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the user-facing assessment API.
/// </summary>
public class AssessmentConsumer( HttpClient http ) : ApiClientBase( http ), IAssessmentConsumer
{
    public async Task<PagedResult<AssessmentDefinitionDto>> GetAssessmentsAsync( int page = 1, int pageSize = 10, string lang = "en" )
        => await GetAsync<PagedResult<AssessmentDefinitionDto>>( $"api/assessments?page={page}&pageSize={pageSize}&lang={lang}" )
           ?? new PagedResult<AssessmentDefinitionDto>( [], 0, page, pageSize );

    public Task<AssessmentDefinitionDto?> GetAssessmentAsync( Guid id, string lang = "en" )
        => GetAsync<AssessmentDefinitionDto>( $"api/assessments/{id}?lang={lang}" );

    public Task<AssessmentSubmissionDto?> SubmitAssessmentAsync( Guid id, SubmitAssessmentRequest request )
        => PostJsonAsync<SubmitAssessmentRequest, AssessmentSubmissionDto>( $"api/assessments/{id}/submit", request );

    public async Task<List<AssessmentSubmissionDto>> GetMySubmissionsAsync()
        => await GetAsync<List<AssessmentSubmissionDto>>( "api/assessments/mine" ) ?? [];
}
