using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class AssistantServiceTests
{
    private const string UserId = "user-1";

    private static (AssistantService svc, IChatClient chatClient, IAssistantRetrievalService retrieval, ISubscriptionService subs)
        NewService(ApplicationDbContext db, AssistantOptions? options = null, AssistantAvailability availability = AssistantAvailability.Ready)
    {
        var chatClient = Substitute.For<IChatClient>();
        var retrieval = Substitute.For<IAssistantRetrievalService>();
        var subs = Substitute.For<ISubscriptionService>();
        subs.GetUserTierAsync(UserId, Arg.Any<CancellationToken>()).Returns(SubscriptionTier.Pro);
        retrieval.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AssistantRetrievedChunk>>([]));

        var runtimeState = new AssistantRuntimeState();
        runtimeState.Set(availability);

        var svc = new AssistantService(
            db, chatClient, retrieval, subs,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options ?? new AssistantOptions()),
            runtimeState,
            NullLogger<AssistantService>.Instance);

        return (svc, chatClient, retrieval, subs);
    }

    private static AssistantChatRequest OneUserMessage(string content) =>
        new([new AssistantMessageDto("user", content)]);

    private static async IAsyncEnumerable<ChatResponseUpdate> Updates(params string[] tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, token);
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> UpdatesThenThrow(string firstToken)
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, firstToken);
        throw new InvalidOperationException("model unavailable");
    }

    [Fact]
    public async Task StreamChatAsync_PassesThroughTokensInOrderThenSourcesThenDone()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, chatClient, _, _) = NewService(db);
        chatClient.GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(Updates("Hello", " world"));

        var events = await Collect(svc.StreamChatAsync(UserId, OneUserMessage("hi"), "en"));

        events.Select(e => e.Kind).ShouldBe(["token", "token", "sources", "done"]);
        events[0].Text.ShouldBe("Hello");
        events[1].Text.ShouldBe(" world");
        events[2].Sources.ShouldBeEmpty();
    }

    [Fact]
    public async Task StreamChatAsync_SystemPromptIncludesLanguageTierEnrollmentsAndContext()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, chatClient, retrieval, _) = NewService(db);
        retrieval.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AssistantRetrievedChunk>>(
                [new AssistantRetrievedChunk("Career Discovery is a course about finding your path.", "Career Discovery", "courses/abc")]));

        db.Enrollments.Add(new Domain.Entities.Enrollment
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            CourseId = Guid.NewGuid(),
            Course = new Domain.Entities.Course { Id = Guid.NewGuid(), TitleEn = "Interview Mastery", IsPublished = true }
        });
        await db.SaveChangesAsync();

        List<ChatMessage>? captured = null;
        chatClient.GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<IEnumerable<ChatMessage>>().ToList();
                return Updates("ok");
            });

        await Collect(svc.StreamChatAsync(UserId, OneUserMessage("What should I take next?"), "el"));

        captured.ShouldNotBeNull();
        var systemMessage = captured![0];
        systemMessage.Role.ShouldBe(ChatRole.System);
        systemMessage.Text.ShouldContain("Greek");
        systemMessage.Text.ShouldContain("Pro");
        systemMessage.Text.ShouldContain("Interview Mastery");
        systemMessage.Text.ShouldContain("Career Discovery is a course about finding your path.");
        captured[1].Role.ShouldBe(ChatRole.User);
        captured[1].Text.ShouldBe("What should I take next?");
    }

    [Fact]
    public async Task StreamChatAsync_RetrievalThrows_StillAnswersWithEmptySources()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, chatClient, retrieval, _) = NewService(db);
        retrieval.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("index unavailable"));
        chatClient.GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(Updates("fallback answer"));

        var events = await Collect(svc.StreamChatAsync(UserId, OneUserMessage("hi"), "en"));

        events.Select(e => e.Kind).ShouldBe(["token", "sources", "done"]);
        events[0].Text.ShouldBe("fallback answer");
        events[1].Sources.ShouldBeEmpty();
    }

    [Fact]
    public async Task StreamChatAsync_ChatStreamThrowsMidway_EmitsErrorEventAndStops()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, chatClient, _, _) = NewService(db);
        chatClient.GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(UpdatesThenThrow("partial"));

        var events = await Collect(svc.StreamChatAsync(UserId, OneUserMessage("hi"), "en"));

        events.Select(e => e.Kind).ShouldBe(["token", "error"]);
        events[0].Text.ShouldBe("partial");
    }

    [Theory]
    [InlineData(AssistantAvailability.OllamaUnreachable)]
    [InlineData(AssistantAvailability.DownloadingModels)]
    public async Task StreamChatAsync_NotReady_EmitsWarmingUpErrorWithoutCallingModel(AssistantAvailability availability)
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, chatClient, _, _) = NewService(db, availability: availability);

        var events = await Collect(svc.StreamChatAsync(UserId, OneUserMessage("hi"), "en"));

        events.Select(e => e.Kind).ShouldBe(["error"]);
        events[0].Text.ShouldNotBeNullOrEmpty();
        chatClient.DidNotReceiveWithAnyArgs().GetStreamingResponseAsync(default!, default, default);
    }

    [Theory]
    [InlineData(AssistantAvailability.OllamaUnreachable, "OllamaUnreachable")]
    [InlineData(AssistantAvailability.DownloadingModels, "DownloadingModels")]
    public async Task GetStatusAsync_NotReady_ReportsRuntimeStateWithoutPinging(AssistantAvailability availability, string expectedState)
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, chatClient, _, _) = NewService(db, availability: availability);

        var status = await svc.GetStatusAsync();

        status.Available.ShouldBeFalse();
        status.State.ShouldBe(expectedState);
        chatClient.DidNotReceiveWithAnyArgs().GetResponseAsync(default(IEnumerable<ChatMessage>)!, default, default);
    }

    [Fact]
    public async Task GetStatusAsync_ReadyButPingFails_FlipsStateToUnreachable()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, chatClient, _, _) = NewService(db); // Ready
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("down"));

        var status = await svc.GetStatusAsync();

        status.Available.ShouldBeFalse();
        status.State.ShouldBe("OllamaUnreachable");
    }

    private static async Task<List<AssistantStreamEvent>> Collect(IAsyncEnumerable<AssistantStreamEvent> source)
    {
        var list = new List<AssistantStreamEvent>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}
