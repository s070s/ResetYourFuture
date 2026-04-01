using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the user-facing assessment API.
/// </summary>
public class AssessmentConsumer : IAssessmentConsumer
{
    private readonly HttpClient _http;

    public AssessmentConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<AssessmentDefinitionDto>> GetAssessmentsAsync( int page = 1, int pageSize = 10, string lang = "en" )
    {
        var response = await _http.GetAsync( $"api/assessments?page={page}&pageSize={pageSize}&lang={lang}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<AssessmentDefinitionDto>>()
              ?? new PagedResult<AssessmentDefinitionDto>( [], 0, page, pageSize )
            : new PagedResult<AssessmentDefinitionDto>( [], 0, page, pageSize );
    }

    public async Task<AssessmentDefinitionDto?> GetAssessmentAsync( Guid id, string lang = "en" )
    {
        var response = await _http.GetAsync( $"api/assessments/{id}?lang={lang}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AssessmentDefinitionDto>()
            : null;
    }

    public async Task<AssessmentSubmissionDto?> SubmitAssessmentAsync( Guid id, SubmitAssessmentRequest request )
    {
        var response = await _http.PostAsJsonAsync( $"api/assessments/{id}/submit", request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AssessmentSubmissionDto>()
            : null;
    }

    public async Task<List<AssessmentSubmissionDto>> GetMySubmissionsAsync()
    {
        var response = await _http.GetAsync( "api/assessments/mine" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<AssessmentSubmissionDto>>() ?? []
            : [];
    }
}
