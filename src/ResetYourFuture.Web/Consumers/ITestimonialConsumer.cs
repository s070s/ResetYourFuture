using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for the public testimonials API.
/// Returns TestimonialDto where AvatarUrl is already a fully-qualified URL.
/// </summary>
public interface ITestimonialConsumer
{
    Task<IReadOnlyList<TestimonialDto>?> GetActiveAsync( CancellationToken ct = default );
}
