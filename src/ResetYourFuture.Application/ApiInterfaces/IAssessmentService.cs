using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Student-facing assessment discovery and submission, gated by subscription entitlements.
/// </summary>
public interface IAssessmentService
{
    Task<ServiceResult<PagedResult<AssessmentDefinitionDto>>> GetPublishedAssessmentsAsync(
        string userId, int page, int pageSize, string lang, Guid? categoryId = null, string? search = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AssessmentDefinitionDto>> GetAssessmentAsync(
        string userId, Guid id, string lang, CancellationToken cancellationToken = default);

    Task<ServiceResult<AssessmentSubmissionDto>> SubmitAssessmentAsync(
        string userId, Guid id, SubmitAssessmentRequest request, CancellationToken cancellationToken = default);

    Task<List<AssessmentSubmissionDto>> GetMySubmissionsAsync(string userId, CancellationToken cancellationToken = default);
}
