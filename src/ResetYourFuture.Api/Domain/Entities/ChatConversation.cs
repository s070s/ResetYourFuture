using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Domain.Entities;

/// <summary>
/// A chat conversation between two users (any role combination).
/// CreatorId is the user who initiated the conversation.
/// ParticipantId is the other user.
/// </summary>
public class ChatConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The user who created / initiated this conversation.
    /// </summary>
    public required string CreatorId { get; set; }
    public ApplicationUser? Creator { get; set; }

    /// <summary>
    /// The other participant in this conversation.
    /// </summary>
    public required string ParticipantId { get; set; }
    public ApplicationUser? Participant { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Cached for listing queries; updated when a new message is sent.
    /// </summary>
    public string? LastMessageContent { get; set; }
    public DateTime? LastMessageAt { get; set; }

    // --- Navigation ---
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
