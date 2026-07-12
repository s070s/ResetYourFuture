using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Admin CRUD operations for categories.
/// </summary>
public interface IAdminCategoryService
{
    Task<PagedResult<AdminCategoryDto>> GetCategoriesAsync(int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default);
    Task<List<CategoryOptionDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdminCategoryDto>> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdminCategoryDto>> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
}
