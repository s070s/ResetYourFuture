using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Web.Pages;
using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// bUnit component tests for Chat (TEST-4) — specifically the SignalR event-handling logic
/// (HandleMessageReceived's unread-count/reorder/append behavior) that no other test exercises,
/// since ChatHub's server side is covered by ChatHubTests but the client-side reaction to
/// IChatService.OnMessageReceived never runs outside a live browser.
/// </summary>
public class ChatComponentTests : BunitContext
{
    private const string Me = "user-me";
    private readonly IChatService _chatService = Substitute.For<IChatService>();
    private readonly ICallService _callService = Substitute.For<ICallService>();

    public ChatComponentTests()
    {
        Services.AddSingleton(_chatService);
        Services.AddSingleton(_callService);
        Services.AddSingleton(new PresenceService(_callService));
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var authStateProvider = Substitute.For<AuthenticationStateProvider>();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me)], "test");
        authStateProvider.GetAuthenticationStateAsync().Returns(
            new AuthenticationState(new ClaimsPrincipal(identity)));
        Services.AddSingleton(authStateProvider);
    }

    private static ChatConversationDto Conversation(string idSeed, int unread = 0) => new(
        Guid.Parse($"00000000-0000-0000-0000-{idSeed.PadLeft(12, '0')}"),
        $"other-{idSeed}",
        $"User {idSeed}",
        "Student",
        LastMessageContent: "hi",
        LastMessageAt: DateTime.UtcNow.AddMinutes(-10),
        UnreadCount: unread);

    private static ChatMessageDto Message(Guid conversationId, string senderId, string content) => new(
        Guid.NewGuid(),
        conversationId,
        senderId,
        "Sender",
        "Student",
        content,
        DateTime.UtcNow,
        IsRead: false);

    private static PagedResult<ChatConversationDto> Conversations(params ChatConversationDto[] items) =>
        new([.. items], items.Length, 1, 10);

    private static PagedResult<ChatMessageDto> Messages(params ChatMessageDto[] items) =>
        new([.. items], items.Length, 1, 20);

    [Fact]
    public void Initialized_StartsChatServiceAndLoadsConversations()
    {
        var conv = Conversation("1");
        _chatService.GetConversationsAsync(1, 10).Returns(Conversations(conv));

        var cut = Render<Chat>();

        cut.Find(".conversation-item .convo-name").TextContent.ShouldContain("User 1");
        _chatService.Received(1).StartAsync(Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public void MessageReceived_ForOtherConversation_IncrementsUnreadBadge()
    {
        var watched = Conversation("1", unread: 0);
        var other = Conversation("2", unread: 0);
        _chatService.GetConversationsAsync(1, 10).Returns(Conversations(watched, other));

        var cut = Render<Chat>();
        cut.Find(".conversation-item").ShouldNotBeNull();

        cut.InvokeAsync(() =>
            _chatService.OnMessageReceived += Raise.Event<Action<ChatMessageDto>>(
                Message(other.Id, "someone-else", "new message")));

        var badges = cut.FindAll(".badge.bg-danger");
        badges.Count.ShouldBe(1);
        badges[0].TextContent.ShouldBe("1");
    }

    [Fact]
    public void MessageReceived_ForOtherConversation_MovesItToTopOfList()
    {
        var first = Conversation("1");
        var second = Conversation("2");
        _chatService.GetConversationsAsync(1, 10).Returns(Conversations(first, second));

        var cut = Render<Chat>();
        cut.FindAll(".conversation-item")[0].TextContent.ShouldContain("User 1");

        cut.InvokeAsync(() =>
            _chatService.OnMessageReceived += Raise.Event<Action<ChatMessageDto>>(
                Message(second.Id, "someone-else", "hey")));

        cut.FindAll(".conversation-item")[0].TextContent.ShouldContain("User 2");
    }

    [Fact]
    public void MessageReceived_ForSelectedConversation_AppendsMessageWithoutIncrementingUnread()
    {
        var conv = Conversation("1");
        _chatService.GetConversationsAsync(1, 10).Returns(Conversations(conv));
        _chatService.GetMessagesAsync(conv.Id, 1, 20).Returns(Messages());

        var cut = Render<Chat>();
        cut.Find(".conversation-item").Click();

        cut.InvokeAsync(() =>
            _chatService.OnMessageReceived += Raise.Event<Action<ChatMessageDto>>(
                Message(conv.Id, "someone-else", "live message")));

        cut.Markup.ShouldContain("live message");
        cut.FindAll(".badge.bg-danger").ShouldBeEmpty();
        _chatService.Received().MarkAsReadAsync(conv.Id);
    }

    [Fact]
    public void MessageReceived_FromCurrentUser_DoesNotMarkAsReadAgain()
    {
        var conv = Conversation("1");
        _chatService.GetConversationsAsync(1, 10).Returns(Conversations(conv));
        _chatService.GetMessagesAsync(conv.Id, 1, 20).Returns(Messages());

        var cut = Render<Chat>();
        cut.Find(".conversation-item").Click();
        _chatService.ClearReceivedCalls();

        cut.InvokeAsync(() =>
            _chatService.OnMessageReceived += Raise.Event<Action<ChatMessageDto>>(
                Message(conv.Id, Me, "my own echoed message")));

        cut.Markup.ShouldContain("my own echoed message");
        _chatService.DidNotReceive().MarkAsReadAsync(Arg.Any<Guid>());
    }
}
