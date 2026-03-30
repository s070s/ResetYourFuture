using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// Client consumer for the admin testimonials management API.
/// </summary>
public interface IAdminTestimonialConsumer
{
    Task<PagedResult<AdminTestimonialDto>?> GetAllAsync( int page = 1, int pageSize = 10, CancellationToken ct = default );
    Task<AdminTestimonialDto?> GetByIdAsync( Guid id, CancellationToken ct = default );
    Task<AdminTestimonialDto?> CreateAsync( SaveTestimonialRequest request, CancellationToken ct = default );
    Task<AdminTestimonialDto?> UpdateAsync( Guid id, SaveTestimonialRequest request, CancellationToken ct = default );
    Task<AdminTestimonialDto?> ToggleActiveAsync( Guid id, CancellationToken ct = default );
    Task<bool> MoveUpAsync( Guid id, CancellationToken ct = default );
    Task<bool> MoveDownAsync( Guid id, CancellationToken ct = default );
    Task<string?> UploadAvatarAsync( Guid id, IBrowserFile file, CancellationToken ct = default );
    Task<bool> RemoveAvatarAsync( Guid id, CancellationToken ct = default );
    Task<bool> DeleteAsync( Guid id, CancellationToken ct = default );
}
