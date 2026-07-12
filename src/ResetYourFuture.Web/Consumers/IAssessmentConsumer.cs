using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for user-facing assessment API operations.
/// </summary>
public interface IAssessmentConsumer
{
    Task<PagedResult<AssessmentDefinitionDto>> GetAssessmentsAsync(int page = 1, int pageSize = 10, string lang = "en", Guid? categoryId = null, string? search = null);
    Task<AssessmentDefinitionDto?> GetAssessmentAsync(Guid id, string lang = "en");
    Task<AssessmentSubmissionDto?> SubmitAssessmentAsync(Guid id, SubmitAssessmentRequest request);
    Task<PagedResult<AssessmentSubmissionDto>?> GetMySubmissionsAsync(int page = 1, int pageSize = 10, string sortBy = "submittedat", string sortDir = "desc");
}
