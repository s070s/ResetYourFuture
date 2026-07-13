using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>Client consumer for admin learning-path management API operations.</summary>
public interface IAdminLearningPathConsumer
{
    Task<PagedResult<AdminLearningPathDto>?> GetAllAsync(int page = 1, int pageSize = 10, string sortBy = "displayorder", string sortDir = "asc");
    Task<AdminLearningPathDetailDto?> GetByIdAsync(Guid id);
    Task<AdminLearningPathDetailDto?> CreateAsync(SaveLearningPathRequest request);
    Task<AdminLearningPathDetailDto?> UpdateAsync(Guid id, SaveLearningPathRequest request);
    Task<bool> PublishAsync(Guid id);
    Task<bool> UnpublishAsync(Guid id);
    Task<bool> DeleteAsync(Guid id);
    Task<AdminLearningPathDetailDto?> AddStepAsync(Guid id, Guid courseId);
    Task<bool> RemoveStepAsync(Guid id, Guid stepId);
    Task<bool> MoveStepUpAsync(Guid id, Guid stepId);
    Task<bool> MoveStepDownAsync(Guid id, Guid stepId);
}
