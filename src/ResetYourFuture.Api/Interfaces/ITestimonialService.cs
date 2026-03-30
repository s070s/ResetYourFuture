using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Api.Interfaces;

/// <summary>
/// Service contract for testimonial operations.
/// </summary>
public interface ITestimonialService
{
    /// <summary>Returns all active testimonials ordered by DisplayOrder for the public landing page.</summary>
    Task<IReadOnlyList<AdminTestimonialDto>> GetActiveAsync( CancellationToken cancellationToken = default );

    /// <summary>Returns all testimonials paginated for the admin list.</summary>
    Task<PagedResult<AdminTestimonialDto>> GetAllForAdminAsync( int page, int pageSize, CancellationToken cancellationToken = default );

    /// <summary>Returns a single testimonial by id, or null if not found.</summary>
    Task<AdminTestimonialDto?> GetByIdAsync( Guid id, CancellationToken cancellationToken = default );

    /// <summary>Creates a new testimonial. DisplayOrder defaults to max+1 if not specified.</summary>
    Task<AdminTestimonialDto> CreateAsync( SaveTestimonialRequest request, CancellationToken cancellationToken = default );

    /// <summary>Updates an existing testimonial. Returns null if not found.</summary>
    Task<AdminTestimonialDto?> UpdateAsync( Guid id, SaveTestimonialRequest request, CancellationToken cancellationToken = default );

    /// <summary>Toggles IsActive. Returns null if not found.</summary>
    Task<AdminTestimonialDto?> ToggleActiveAsync( Guid id, CancellationToken cancellationToken = default );

    /// <summary>Swaps DisplayOrder with the next-lower-order item. Returns false if not found or already first.</summary>
    Task<bool> MoveUpAsync( Guid id, CancellationToken cancellationToken = default );

    /// <summary>Swaps DisplayOrder with the next-higher-order item. Returns false if not found or already last.</summary>
    Task<bool> MoveDownAsync( Guid id, CancellationToken cancellationToken = default );

    /// <summary>Updates the AvatarPath after a successful file upload. Returns null if not found.</summary>
    Task<AdminTestimonialDto?> SetAvatarPathAsync( Guid id, string path, CancellationToken cancellationToken = default );

    /// <summary>Clears the AvatarPath. Returns false if not found.</summary>
    Task<bool> RemoveAvatarAsync( Guid id, CancellationToken cancellationToken = default );

    /// <summary>Hard-deletes a testimonial. Returns false if not found.</summary>
    Task<bool> DeleteAsync( Guid id, CancellationToken cancellationToken = default );
}
