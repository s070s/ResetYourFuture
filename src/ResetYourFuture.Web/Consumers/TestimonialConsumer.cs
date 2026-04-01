using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the public testimonials API.
/// Transforms the raw AvatarPath into a fully-qualified media URL so Blazor
/// components can use it directly as an img src without knowing the API base address.
/// </summary>
public class TestimonialConsumer : ITestimonialConsumer
{
    private readonly HttpClient _http;

    public TestimonialConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<IReadOnlyList<TestimonialDto>?> GetActiveAsync( CancellationToken ct = default )
    {
        var response = await _http.GetAsync( "api/testimonials", ct );
        if ( !response.IsSuccessStatusCode )
            return null;

        var items = await response.Content.ReadFromJsonAsync<List<AdminTestimonialDto>>( ct );
        if ( items is null )
            return null;

        var apiBase = _http.BaseAddress?.ToString().TrimEnd( '/' ) ?? string.Empty;

        return items.Select( t => new TestimonialDto(
            t.Id,
            t.FullName,
            t.RoleOrTitle,
            t.CompanyOrContext,
            t.QuoteText,
            BuildAvatarUrl( t.AvatarPath, apiBase ),
            t.DisplayOrder
        ) ).ToList();
    }

    private static string? BuildAvatarUrl( string? avatarPath, string apiBase )
    {
        if ( string.IsNullOrWhiteSpace( avatarPath ) )
            return null;

        // If it's already a full URL, return as-is
        if ( avatarPath.StartsWith( "http", StringComparison.OrdinalIgnoreCase ) )
            return avatarPath;

        return $"{apiBase}/api/media/{avatarPath.TrimStart( '/' )}";
    }
}
