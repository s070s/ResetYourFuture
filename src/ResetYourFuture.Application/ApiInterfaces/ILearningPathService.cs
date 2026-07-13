using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Ordered course sequences ("Career Change Starter → CV Lab → Interview Mastery"). Progress is
/// projected from the existing Enrollment/LessonCompletion data — no dedicated progress table.
/// </summary>
public interface ILearningPathService
{
    /// <summary>Published paths for the catalog, ordered by DisplayOrder. CompletedSteps is 0 for anonymous callers.</summary>
    Task<IReadOnlyList<LearningPathListItemDto>> GetPublishedAsync(string? userId, string lang, CancellationToken cancellationToken = default);

    /// <summary>A published path with per-step progression state. Null if not found or unpublished.</summary>
    Task<LearningPathDetailDto?> GetByIdAsync(Guid id, string? userId, string lang, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminLearningPathDto>> GetAllForAdminAsync(
        int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default);

    Task<AdminLearningPathDetailDto?> GetAdminByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdminLearningPathDetailDto> CreateAsync(SaveLearningPathRequest request, CancellationToken cancellationToken = default);

    Task<AdminLearningPathDetailDto?> UpdateAsync(Guid id, SaveLearningPathRequest request, CancellationToken cancellationToken = default);

    Task<bool> PublishAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> UnpublishAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Appends a course as the last step. Fails if the course is already a step in this path.</summary>
    Task<ServiceResult<AdminLearningPathDetailDto>> AddStepAsync(Guid pathId, Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>Removes a step and re-sequences the remaining steps to stay contiguous.</summary>
    Task<bool> RemoveStepAsync(Guid pathId, Guid stepId, CancellationToken cancellationToken = default);

    Task<bool> MoveStepUpAsync(Guid pathId, Guid stepId, CancellationToken cancellationToken = default);

    Task<bool> MoveStepDownAsync(Guid pathId, Guid stepId, CancellationToken cancellationToken = default);
}
