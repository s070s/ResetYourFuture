using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for public category discovery (browse/filter chips).
/// </summary>
public interface ICategoryConsumer
{
    Task<List<CategoryDto>> GetCategoriesAsync(string scope, string lang = "en");
}
