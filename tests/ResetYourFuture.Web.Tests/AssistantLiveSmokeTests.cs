using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ResetYourFuture.Application.DTOs;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// Live smoke test against a locally running Ollama — guarded by RYF_OLLAMA_LIVE=1 so it
/// self-passes (effectively skips) in CI and on machines without Ollama. Run with:
///   RYF_OLLAMA_LIVE=1 dotnet test --filter AssistantLiveSmoke
/// Boots its own factory with the assistant enabled (the shared "web" host keeps it off),
/// waits for the bootstrap to reach Ready, then asserts one real chat turn streams tokens
/// and finishes with a done event.
/// </summary>
public class AssistantLiveSmokeTests(ITestOutputHelper output)
{
    private static bool LiveEnabled =>
        Environment.GetEnvironmentVariable("RYF_OLLAMA_LIVE") == "1";

    private sealed class LiveAssistantFactory : CustomWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Re-enable the assistant for this host only (the base factory pins the
            // environment variable off for every other test).
            builder.UseSetting("Assistant:Enabled", "true");
        }
    }

    [Fact]
    public async Task Chat_AgainstLocalOllama_StreamsTokensAndCompletes()
    {
        if (!LiveEnabled)
        {
            output.WriteLine("RYF_OLLAMA_LIVE != 1 — live smoke test skipped.");
            return;
        }

        await using var factory = new LiveAssistantFactory();
        var client = await factory.CreateAuthenticatedClientAsync("Student");
        client.Timeout = TimeSpan.FromMinutes(5);

        // Wait for the bootstrap supervisor to report Ready (models may still be pulling).
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(4);
        AssistantStatusDto? status = null;
        while (DateTime.UtcNow < deadline)
        {
            status = await client.GetFromJsonAsync<AssistantStatusDto>("/api/assistant/status");
            output.WriteLine($"status: {status?.State} {status?.Progress}");
            if (status is { State: "Ready" })
                break;
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        status.ShouldNotBeNull();
        status!.State.ShouldBe("Ready", $"assistant never became Ready (last: {status.State} {status.Progress})");

        var response = await client.PostAsJsonAsync("/api/assistant/chat",
            new AssistantChatRequest([new AssistantMessageDto("user", "What courses do you offer?")]));
        response.EnsureSuccessStatusCode();

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = SseParser.Create(stream, (string _, ReadOnlySpan<byte> data) =>
            JsonSerializer.Deserialize<AssistantStreamEvent>(data, jsonOptions)!);

        var kinds = new List<string>();
        var text = new System.Text.StringBuilder();
        await foreach (var item in parser.EnumerateAsync())
        {
            kinds.Add(item.Data.Kind);
            if (item.Data.Kind == "token")
                text.Append(item.Data.Text);
        }

        output.WriteLine($"events: {string.Join(",", kinds.Distinct())}; answer: {text.ToString()[..Math.Min(200, text.Length)]}");
        kinds.ShouldContain("token");
        kinds.Last().ShouldBe("done");
        text.Length.ShouldBeGreaterThan(0);
    }
}
