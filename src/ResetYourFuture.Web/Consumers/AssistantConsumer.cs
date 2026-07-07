using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the AI assistant API. Unlike the other consumers, <see cref="StreamChatAsync"/>
/// is bespoke rather than built on <see cref="ApiClientBase"/>'s JSON helpers, since it reads a
/// Server-Sent Events stream instead of a single response body.
/// </summary>
public class AssistantConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider)
    : ApiClientBase(http, tokenProvider), IAssistantConsumer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<AssistantStreamEvent> StreamChatAsync(
        AssistantChatRequest request, string lang, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizationAsync();

        HttpResponseMessage? response = null;
        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"api/assistant/chat?lang={lang}")
            {
                Content = JsonContent.Create(request)
            };
            response = await Http.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // response stays null — handled below, outside the catch (yield isn't allowed in one).
        }

        // A null response (network failure) or non-success status (including an empty-bodied 429
        // from the "assistant" rate limiter) can't be parsed as an SSE stream — surface a plain error.
        if (response is null || !response.IsSuccessStatusCode)
        {
            response?.Dispose();
            yield return new AssistantStreamEvent("error", "Could not reach the assistant.");
            yield break;
        }

        using (response)
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var parser = SseParser.Create(stream, (string _, ReadOnlySpan<byte> data) =>
                JsonSerializer.Deserialize<AssistantStreamEvent>(data, JsonOptions)!);

            await foreach (var item in parser.EnumerateAsync(cancellationToken))
                yield return item.Data;
        }
    }

    public Task<AssistantStatusDto?> GetStatusAsync() => GetAsync<AssistantStatusDto>("api/assistant/status");
}
