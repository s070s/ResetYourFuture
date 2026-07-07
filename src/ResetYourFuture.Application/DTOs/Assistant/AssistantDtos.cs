using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Application.DTOs;

/// <summary>One turn in the assistant conversation, as replayed by the client on every request
/// (no server-side conversation persistence — see AI_ASSISTANT_PLAN.md D7).</summary>
public record AssistantMessageDto(
    [Required, MaxLength(20)] string Role,
    [Required, MaxLength(4000)] string Content
);

public record AssistantChatRequest(
    [Required, MinLength(1), MaxLength(20)] List<AssistantMessageDto> Messages
);

public record AssistantSourceDto(string Title, string Url);

/// <summary>One Server-Sent Event in an assistant chat stream. Kind is "token" | "sources" | "done" | "error".</summary>
public record AssistantStreamEvent(string Kind, string? Text = null, List<AssistantSourceDto>? Sources = null);

public record AssistantStatusDto(bool Available, string? Model);
