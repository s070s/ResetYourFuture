using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for admin category management API operations.
/// </summary>
public interface IAdminCategoryConsumer
{
    Task<PagedResult<AdminCategoryDto>?> GetCategoriesAsync(int page = 1, int pageSize = 10);
    Task<List<CategoryOptionDto>> GetAllCategoriesAsync();
    Task<AdminCategoryDto?> CreateCategoryAsync(SaveCategoryRequest request);
    Task<AdminCategoryDto?> UpdateCategoryAsync(Guid id, SaveCategoryRequest request);
    Task<bool> DeleteCategoryAsync(Guid id);
}
