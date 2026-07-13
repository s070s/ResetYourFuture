using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// A persisted, per-user notification. Stores a resx key (<see cref="TitleKey"/>) and its
/// format arguments rather than pre-rendered text, so the same row renders correctly in
/// whichever culture the recipient is viewing it in — including on a different day than it
/// was raised, in the other language than the actor who triggered it used.
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public NotificationType Type { get; set; }

    /// <summary>Resx key in NotificationRes (e.g. "ChatMessageReceived") resolved client-side.</summary>
    public required string TitleKey { get; set; }

    /// <summary>JSON string array of arguments applied to the resolved template with string.Format.</summary>
    public string? BodyArgsJson { get; set; }

    /// <summary>Relative app URL to navigate to when the notification is clicked.</summary>
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
