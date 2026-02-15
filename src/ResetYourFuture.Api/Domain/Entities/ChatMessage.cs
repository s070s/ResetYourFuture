using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

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

    public required string Content { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }
}
