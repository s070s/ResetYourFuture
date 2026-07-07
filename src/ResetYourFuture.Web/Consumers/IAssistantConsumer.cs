using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for the AI assistant API.
/// </summary>
public interface IAssistantConsumer
{
    IAsyncEnumerable<AssistantStreamEvent> StreamChatAsync(AssistantChatRequest request, string lang, CancellationToken cancellationToken = default);
    Task<AssistantStatusDto?> GetStatusAsync();
}
