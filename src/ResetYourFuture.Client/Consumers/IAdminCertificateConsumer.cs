using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// Client consumer for admin certificate management API operations.
/// </summary>
public interface IAdminCertificateConsumer
{
    Task<PagedResult<AdminCertificateListItemDto>?> GetCertificatesAsync( int page = 1, int pageSize = 20 );
    Task<bool> RevokeAsync( Guid certificateId );
    Task<bool> RegenerateAsync( Guid certificateId );
}
