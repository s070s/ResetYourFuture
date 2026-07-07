using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// A single message within a chat conversation.
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }
    public ChatConversation? Conversation { get; set; }

    /// <summary>
    /// The user who sent this message (Admin or Student).
    /// </summary>
    public required string SenderId { get; set; }
    public ApplicationUser? Sender { get; set; }

    /// <summary>
    /// Plain-English fallback for the sidebar's LastMessageContent cache when this is a call
    /// event ("Missed call", "Video call ended · 12:34"). The chat pane itself renders call
    /// events from <see cref="CallEvent"/> at display time so the text stays localized.
    /// </summary>
    public required string Content { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }

    /// <summary>
    /// Non-null when this row represents a call event rather than a user-typed message.
    /// A single 1:1 call session yields up to two rows (Started, then Ended/Missed).
    /// </summary>
    public Guid? CallSessionId { get; set; }
    public CallSession? CallSession { get; set; }

    public CallEventKind? CallEvent { get; set; }
}
