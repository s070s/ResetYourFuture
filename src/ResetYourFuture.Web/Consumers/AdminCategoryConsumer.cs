using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin category management API.
/// </summary>
public class AdminCategoryConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider) : ApiClientBase(http, tokenProvider), IAdminCategoryConsumer
{
    public Task<PagedResult<AdminCategoryDto>?> GetCategoriesAsync(int page = 1, int pageSize = 10, string sortBy = "nameen", string sortDir = "asc")
        => GetAsync<PagedResult<AdminCategoryDto>>($"api/admin/categories?page={page}&pageSize={pageSize}&sortBy={sortBy}&sortDir={sortDir}");

    public async Task<List<CategoryOptionDto>> GetAllCategoriesAsync()
        => await GetAsync<List<CategoryOptionDto>>("api/admin/categories/all") ?? [];

    public Task<AdminCategoryDto?> CreateCategoryAsync(SaveCategoryRequest request)
        => PostJsonAsync<SaveCategoryRequest, AdminCategoryDto>("api/admin/categories", request);

    public Task<AdminCategoryDto?> UpdateCategoryAsync(Guid id, SaveCategoryRequest request)
        => PutJsonAsync<SaveCategoryRequest, AdminCategoryDto>($"api/admin/categories/{id}", request);

    public Task<bool> DeleteCategoryAsync(Guid id)
        => DeleteAsync($"api/admin/categories/{id}");
}
