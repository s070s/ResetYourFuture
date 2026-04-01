using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin module management API.
/// </summary>
public class AdminModuleConsumer : IAdminModuleConsumer
{
    private readonly HttpClient _http;

    public AdminModuleConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<List<AdminModuleDto>> GetModulesByCourseAsync( Guid courseId )
    {
        var response = await _http.GetAsync( $"api/admin/modules/course/{courseId}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<AdminModuleDto>>() ?? []
            : [];
    }

    public async Task<AdminModuleDto?> GetModuleAsync( Guid id )
    {
        var response = await _http.GetAsync( $"api/admin/modules/{id}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminModuleDto>()
            : null;
    }

    public async Task<AdminModuleDto?> CreateModuleAsync( SaveModuleRequest request )
    {
        var response = await _http.PostAsJsonAsync( "api/admin/modules" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminModuleDto>()
            : null;
    }

    public async Task<AdminModuleDto?> UpdateModuleAsync( Guid id , SaveModuleRequest request )
    {
        var response = await _http.PutAsJsonAsync( $"api/admin/modules/{id}" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminModuleDto>()
            : null;
    }

    public async Task<bool> DeleteModuleAsync( Guid id )
    {
        var response = await _http.DeleteAsync( $"api/admin/modules/{id}" );
        return response.IsSuccessStatusCode;
    }
}
