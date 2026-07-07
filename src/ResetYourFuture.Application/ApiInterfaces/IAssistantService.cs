using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Orchestrates one grounded, streaming answer from the AI assistant: retrieves supporting
/// content, composes a system prompt (persona + retrieved context + the user's tier/enrollments),
/// and streams the model's reply as Server-Sent Events.
/// </summary>
public interface IAssistantService
{
    IAsyncEnumerable<AssistantStreamEvent> StreamChatAsync(
        string userId, AssistantChatRequest request, string language, CancellationToken cancellationToken = default);

    Task<AssistantStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
