using System.Runtime.CompilerServices;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// No-op <see cref="IAssistantService"/> registered when Assistant:Enabled is false, so the API
/// controllers and widget always resolve and report unavailability instead of the app failing to
/// start or a request 500ing.
/// </summary>
public class DisabledAssistantService : IAssistantService
{
    public async IAsyncEnumerable<AssistantStreamEvent> StreamChatAsync(
        string userId, AssistantChatRequest request, string language,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return new AssistantStreamEvent("error", "The AI assistant is not enabled on this server.");
    }

    public Task<AssistantStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AssistantStatusDto(false, null, "Disabled"));
}
