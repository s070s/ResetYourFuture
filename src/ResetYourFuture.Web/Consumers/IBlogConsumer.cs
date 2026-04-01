using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for the public blog API.
/// </summary>
public interface IBlogConsumer
{
    Task<IReadOnlyList<BlogArticleSummaryDto>?> GetSummariesAsync( int count = 6, string lang = "en", CancellationToken ct = default );
    Task<BlogArticleDto?> GetBySlugAsync( string slug, string lang = "en", CancellationToken ct = default );
}
