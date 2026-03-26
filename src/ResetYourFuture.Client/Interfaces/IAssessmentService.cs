using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Interfaces;

/// <summary>
/// Client service interface for assessment-related API operations.
/// </summary>
public interface IAssessmentService
{
    Task<PagedResult<AssessmentDefinitionDto>> GetAssessmentsAsync( int page = 1, int pageSize = 10, string lang = "en" );
}
