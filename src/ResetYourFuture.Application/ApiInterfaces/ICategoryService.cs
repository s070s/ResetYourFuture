using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Public category discovery for browse/filter chips.
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Returns categories that have at least one published item in the given scope
    /// ("courses" or "assessments"), with a per-category count, language-resolved and name-sorted.
    /// </summary>
    Task<List<CategoryDto>> GetCategoriesAsync(string scope, string lang, CancellationToken cancellationToken = default);
}
