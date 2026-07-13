using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>HTTP consumer for the admin learning-path management API.</summary>
public class AdminLearningPathConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider)
    : ApiClientBase(http, tokenProvider), IAdminLearningPathConsumer
{
    public Task<PagedResult<AdminLearningPathDto>?> GetAllAsync(int page = 1, int pageSize = 10, string sortBy = "displayorder", string sortDir = "asc")
        => GetAsync<PagedResult<AdminLearningPathDto>>($"api/admin/paths?page={page}&pageSize={pageSize}&sortBy={sortBy}&sortDir={sortDir}");

    public Task<AdminLearningPathDetailDto?> GetByIdAsync(Guid id)
        => GetAsync<AdminLearningPathDetailDto>($"api/admin/paths/{id}");

    public Task<AdminLearningPathDetailDto?> CreateAsync(SaveLearningPathRequest request)
        => PostJsonAsync<SaveLearningPathRequest, AdminLearningPathDetailDto>("api/admin/paths", request);

    public Task<AdminLearningPathDetailDto?> UpdateAsync(Guid id, SaveLearningPathRequest request)
        => PutJsonAsync<SaveLearningPathRequest, AdminLearningPathDetailDto>($"api/admin/paths/{id}", request);

    public Task<bool> PublishAsync(Guid id) => ActionAsync($"api/admin/paths/{id}/publish");

    public Task<bool> UnpublishAsync(Guid id) => ActionAsync($"api/admin/paths/{id}/unpublish");

    public Task<bool> DeleteAsync(Guid id) => DeleteAsync($"api/admin/paths/{id}");

    public Task<AdminLearningPathDetailDto?> AddStepAsync(Guid id, Guid courseId)
        => PostJsonAsync<AddLearningPathStepRequest, AdminLearningPathDetailDto>($"api/admin/paths/{id}/steps", new AddLearningPathStepRequest(courseId));

    public Task<bool> RemoveStepAsync(Guid id, Guid stepId) => DeleteAsync($"api/admin/paths/{id}/steps/{stepId}");

    public Task<bool> MoveStepUpAsync(Guid id, Guid stepId) => ActionAsync($"api/admin/paths/{id}/steps/{stepId}/move-up");

    public Task<bool> MoveStepDownAsync(Guid id, Guid stepId) => ActionAsync($"api/admin/paths/{id}/steps/{stepId}/move-down");
}
