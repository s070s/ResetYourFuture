using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// A single video call — either a 1:1 call tied to a chat conversation, or an independent
/// group call. Tracks ring, connect, and end timestamps for duration/history reporting.
/// </summary>
public class CallSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The user who started the call.
    /// </summary>
    public required string InitiatorId { get; set; }
    public ApplicationUser? Initiator { get; set; }

    /// <summary>
    /// Non-null when this call originated from a 1:1 chat conversation. Null for
    /// independent group calls started from the multi-select picker.
    /// </summary>
    public Guid? ConversationId { get; set; }
    public ChatConversation? Conversation { get; set; }

    /// <summary>
    /// When ringing began.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the first participant accepted (call became "live").
    /// </summary>
    public DateTime? ConnectedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public CallEndReason? EndReason { get; set; }

    // --- Navigation ---
    public ICollection<CallParticipant> Participants { get; set; } = [];
}
