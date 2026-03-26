using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// HTTP consumer for the admin course management API.
/// </summary>
public class AdminCourseConsumer : IAdminCourseConsumer
{
    private readonly HttpClient _http;

    public AdminCourseConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<AdminCourseDto>?> GetCoursesAsync( int page = 1 , int pageSize = 10 )
    {
        var response = await _http.GetAsync( $"api/admin/courses?page={page}&pageSize={pageSize}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<AdminCourseDto>>()
            : null;
    }

    public async Task<AdminCourseDto?> GetCourseAsync( Guid id )
    {
        var response = await _http.GetAsync( $"api/admin/courses/{id}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminCourseDto>()
            : null;
    }

    public async Task<AdminCourseDto?> CreateCourseAsync( SaveCourseRequest request )
    {
        var response = await _http.PostAsJsonAsync( "api/admin/courses" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminCourseDto>()
            : null;
    }

    public async Task<AdminCourseDto?> UpdateCourseAsync( Guid id , SaveCourseRequest request )
    {
        var response = await _http.PutAsJsonAsync( $"api/admin/courses/{id}" , request );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminCourseDto>()
            : null;
    }

    public async Task<bool> DeleteCourseAsync( Guid id )
    {
        var response = await _http.DeleteAsync( $"api/admin/courses/{id}" );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PublishCourseAsync( Guid id )
    {
        var response = await _http.PostAsync( $"api/admin/courses/{id}/publish" , null );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnpublishCourseAsync( Guid id )
    {
        var response = await _http.PostAsync( $"api/admin/courses/{id}/unpublish" , null );
        return response.IsSuccessStatusCode;
    }
}
