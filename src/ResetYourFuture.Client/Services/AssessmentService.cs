using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Services;

/// <summary>
/// HTTP implementation of IAssessmentService.
/// </summary>
public class AssessmentService : IAssessmentService
{
    private readonly HttpClient _http;

    public AssessmentService( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<AssessmentDefinitionDto>> GetAssessmentsAsync( int page = 1, int pageSize = 10 )
    {
        var response = await _http.GetAsync( $"api/assessments?page={page}&pageSize={pageSize}" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<PagedResult<AssessmentDefinitionDto>>()
                ?? new PagedResult<AssessmentDefinitionDto>( [], 0, page, pageSize );
        }
        return new PagedResult<AssessmentDefinitionDto>( [], 0, page, pageSize );
    }
}
