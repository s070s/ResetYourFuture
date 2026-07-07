using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using ResetYourFuture.Application.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// The shared "web" test host runs with Assistant:Enabled=false (the appsettings.json default),
/// so IAssistantService resolves to DisabledAssistantService. These tests verify the SSE plumbing
/// and the graceful-unavailable contract end-to-end without needing a real model.
/// </summary>
[Collection("web")]
public class AssistantControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CustomWebAppFactory _factory;

    public AssistantControllerTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Chat_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest([new AssistantMessageDto("user", "hi")]));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Chat_Authenticated_StreamsErrorEventWhenAssistantDisabled()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest([new AssistantMessageDto("user", "hi")]));

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

        var events = await ParseEventsAsync(response);

        events.ShouldHaveSingleItem();
        events[0].Kind.ShouldBe("error");
    }

    [Fact]
    public async Task Chat_TooManyMessages_Returns400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");
        var messages = Enumerable.Range(0, 21).Select(i => new AssistantMessageDto("user", $"msg {i}")).ToList();

        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest(messages));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Status_Authenticated_ReturnsUnavailableWhenAssistantDisabled()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        var status = await client.GetFromJsonAsync<AssistantStatusDto>("/api/assistant/status");

        status.ShouldNotBeNull();
        status!.Available.ShouldBeFalse();
    }

    [Fact]
    public async Task Status_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/assistant/status")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<List<AssistantStreamEvent>> ParseEventsAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = SseParser.Create(stream, (string _, ReadOnlySpan<byte> data) =>
            JsonSerializer.Deserialize<AssistantStreamEvent>(data, JsonOptions)!);

        var events = new List<AssistantStreamEvent>();
        await foreach (var item in parser.EnumerateAsync())
            events.Add(item.Data);
        return events;
    }
}
