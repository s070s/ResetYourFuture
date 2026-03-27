using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// HTTP consumer for admin certificate management API operations.
/// </summary>
public class AdminCertificateConsumer : IAdminCertificateConsumer
{
    private readonly HttpClient _http;

    public AdminCertificateConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<AdminCertificateListItemDto>?> GetCertificatesAsync( int page = 1, int pageSize = 20 )
    {
        var response = await _http.GetAsync( $"api/admin/certificates?page={page}&pageSize={pageSize}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<AdminCertificateListItemDto>>()
            : null;
    }

    public async Task<bool> RevokeAsync( Guid certificateId )
    {
        var response = await _http.PostAsync( $"api/admin/certificates/{certificateId}/revoke", null );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RegenerateAsync( Guid certificateId )
    {
        var response = await _http.PostAsync( $"api/admin/certificates/{certificateId}/regenerate", null );
        return response.IsSuccessStatusCode;
    }
}
