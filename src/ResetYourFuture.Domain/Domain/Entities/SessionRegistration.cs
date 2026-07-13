using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Domain.Entities;

/// <summary>A user's registration for a <see cref="ScheduledSession"/>. One per (Session, User).</summary>
public class SessionRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }
    public ScheduledSession? Session { get; set; }

    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}
