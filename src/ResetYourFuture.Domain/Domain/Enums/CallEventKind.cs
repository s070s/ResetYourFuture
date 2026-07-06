namespace ResetYourFuture.Domain.Enums;

/// <summary>
/// The kind of call event a ChatMessage row represents. Stored as int.
/// Localized text is rendered at display time from this value — never baked into ChatMessage.Content.
/// </summary>
public enum CallEventKind
{
    Started = 1,
    Missed = 2,
    Ended = 3
}
