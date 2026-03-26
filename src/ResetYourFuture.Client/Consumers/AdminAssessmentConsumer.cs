using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// HTTP consumer for the admin assessment management API.
/// </summary>
public class AdminAssessmentConsumer : IAdminAssessmentConsumer
{
    private readonly HttpClient _http;

    public AdminAssessmentConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<AssessmentDefinitionListItemDto>?> GetAssessmentsAsync( int page = 1 , int pageSize = 10 )
    {
        var response = await _http.GetAsync( $"api/admin/assessments?page={page}&pageSize={pageSize}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<AssessmentDefinitionListItemDto>>()
            : null;
    }

    public async Task<AdminAssessmentDefinitionDto?> GetAssessmentAsync( Guid id )
    {
        var response = await _http.GetAsync( $"api/admin/assessments/{id}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminAssessmentDefinitionDto>()
            : null;
    }

    public async Task<AdminAssessmentDefinitionDto?> CreateAssessmentAsync( SaveAssessmentDefinitionRequest request )
    {
        var response = await _http.PostAsJsonAsync( "api/admin/assessments" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminAssessmentDefinitionDto>()
            : null;
    }

    public async Task<AdminAssessmentDefinitionDto?> UpdateAssessmentAsync( Guid id , SaveAssessmentDefinitionRequest request )
    {
        var response = await _http.PutAsJsonAsync( $"api/admin/assessments/{id}" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminAssessmentDefinitionDto>()
            : null;
    }

    public async Task<bool> DeleteAssessmentAsync( Guid id )
    {
        var response = await _http.DeleteAsync( $"api/admin/assessments/{id}" );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PublishAssessmentAsync( Guid id )
    {
        var response = await _http.PostAsync( $"api/admin/assessments/{id}/publish" , null );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnpublishAssessmentAsync( Guid id )
    {
        var response = await _http.PostAsync( $"api/admin/assessments/{id}/unpublish" , null );
        return response.IsSuccessStatusCode;
    }

    public async Task<PagedResult<AssessmentSubmissionListItemDto>?> GetSubmissionsAsync( Guid id , int page = 1 , int pageSize = 10 )
    {
        var response = await _http.GetAsync( $"api/admin/assessments/{id}/submissions?page={page}&pageSize={pageSize}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<AssessmentSubmissionListItemDto>>()
            : null;
    }
}
