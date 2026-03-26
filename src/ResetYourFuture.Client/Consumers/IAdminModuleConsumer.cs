using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// Client consumer for admin module management API operations.
/// </summary>
public interface IAdminModuleConsumer
{
    Task<List<AdminModuleDto>> GetModulesByCourseAsync( Guid courseId );
    Task<AdminModuleDto?> GetModuleAsync( Guid id );
    Task<AdminModuleDto?> CreateModuleAsync( SaveModuleRequest request );
    Task<AdminModuleDto?> UpdateModuleAsync( Guid id , SaveModuleRequest request );
    Task<bool> DeleteModuleAsync( Guid id );
}
