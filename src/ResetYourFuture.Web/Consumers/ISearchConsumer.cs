using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for the site search API.
/// </summary>
public interface ISearchConsumer
{
    Task<SiteSearchResultDto?> SearchAsync(string query, int limit, string lang);
}
