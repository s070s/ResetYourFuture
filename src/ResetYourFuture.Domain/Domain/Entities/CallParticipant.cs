using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// A single user's participation record within a <see cref="CallSession"/>.
/// </summary>
public class CallParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CallSessionId { get; set; }
    public CallSession? CallSession { get; set; }

    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public CallParticipantStatus Status { get; set; } = CallParticipantStatus.Invited;

    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;

    public DateTime? JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }
}
