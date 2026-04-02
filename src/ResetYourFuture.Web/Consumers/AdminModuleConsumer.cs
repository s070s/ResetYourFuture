using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin module management API.
/// </summary>
public class AdminModuleConsumer( HttpClient http ) : ApiClientBase( http ), IAdminModuleConsumer
{
    public async Task<List<AdminModuleDto>> GetModulesByCourseAsync( Guid courseId )
        => await GetAsync<List<AdminModuleDto>>( $"api/admin/modules/course/{courseId}" ) ?? [];

    public Task<AdminModuleDto?> GetModuleAsync( Guid id )
        => GetAsync<AdminModuleDto>( $"api/admin/modules/{id}" );

    public Task<AdminModuleDto?> CreateModuleAsync( SaveModuleRequest request )
        => PostJsonAsync<SaveModuleRequest, AdminModuleDto>( "api/admin/modules", request );

    public Task<AdminModuleDto?> UpdateModuleAsync( Guid id, SaveModuleRequest request )
        => PutJsonAsync<SaveModuleRequest, AdminModuleDto>( $"api/admin/modules/{id}", request );

    public Task<bool> DeleteModuleAsync( Guid id )
        => DeleteAsync( $"api/admin/modules/{id}" );
}
