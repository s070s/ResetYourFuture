using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// A scheduled live group session (office hours, group coaching). A schedule row *materializes*
/// into a real <see cref="Entities.CallSession"/> the moment the first participant starts the
/// call from the /sessions page (existing group-call flow); <see cref="CallSessionId"/> records
/// that linkage once it happens.
/// </summary>
public class ScheduledSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string HostUserId { get; set; }
    public ApplicationUser? Host { get; set; }

    public required string TitleEn { get; set; }
    public string? TitleEl { get; set; }

    public Guid? CourseId { get; set; }
    public Course? Course { get; set; }

    public DateTimeOffset StartsAtUtc { get; set; }

    public int DurationMinutes { get; set; } = 30;

    /// <summary>Registration cap, independent of the WebRTC mesh's own global participant cap.</summary>
    public int MaxParticipants { get; set; } = 6;

    public ScheduledSessionStatus Status { get; set; } = ScheduledSessionStatus.Scheduled;

    /// <summary>Set once someone actually starts the call for this session.</summary>
    public Guid? CallSessionId { get; set; }
    public CallSession? CallSession { get; set; }

    /// <summary>Set once the 15-minutes-before reminder has been dispatched, so it fires only once.</summary>
    public DateTimeOffset? ReminderSentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SessionRegistration> Registrations { get; set; } = [];
}
