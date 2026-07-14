using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class ChatCommandServiceTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    private const string MyName = "F L";
    private const string MyRole = "Admin";

    private static ChatCommandService NewService(ApplicationDbContext db) => new(db);

    private static ApplicationUser AppUser(string id, bool enabled = true) =>
        new() { Id = id, UserName = id, Email = $"{id}@x.com", FirstName = "F", LastName = "L", IsEnabled = enabled };

    private static ChatConversation Conversation(string creator, string participant) =>
        new() { Id = Guid.NewGuid(), CreatorId = creator, ParticipantId = participant };

    // ---- SendMessageAsync ------------------------------------------------------

    [Fact]
    public async Task SendMessage_EmptyContent_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var svc = NewService(db);

        var result = await svc.SendMessageAsync(Me, MyName, MyRole, Guid.NewGuid(), "   ");

        result.StatusCode.ShouldBe(400);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_TooLong_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var svc = NewService(db);

        var result = await svc.SendMessageAsync(Me, MyName, MyRole, Guid.NewGuid(), new string('x', 4001));

        result.StatusCode.ShouldBe(400);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_ConversationMissing_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var svc = NewService(db);

        var result = await svc.SendMessageAsync(Me, MyName, MyRole, Guid.NewGuid(), "hello");

        result.StatusCode.ShouldBe(404);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_NotParticipant_ReturnsForbidden()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation("x", "y");
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var result = await svc.SendMessageAsync(Me, MyName, MyRole, conv.Id, "hello");

        result.StatusCode.ShouldBe(403);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_Valid_PersistsAndReturnsRecipientId()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation(Me, Other);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var result = await svc.SendMessageAsync(Me, MyName, MyRole, conv.Id, "hello world");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RecipientId.ShouldBe(Other);
        result.Value.Message.Content.ShouldBe("hello world");
        result.Value.Message.SenderName.ShouldBe(MyName);
        result.Value.Message.SenderRole.ShouldBe(MyRole);
        (await db.ChatMessages.CountAsync()).ShouldBe(1);
        (await db.ChatConversations.FirstAsync()).LastMessageContent.ShouldBe("hello world");
    }

    // ---- MarkAsReadAsync --------------------------------------------------------

    [Fact]
    public async Task MarkAsRead_FlipsOthersUnreadMessages()
    {
        // ExecuteUpdateAsync is unsupported on InMemory — use SQLite.
        await using var db = DbContextFactory.CreateSqlite();
        db.Users.AddRange(AppUser(Me), AppUser(Other));
        var conv = Conversation(Me, Other);
        db.ChatConversations.Add(conv);
        db.ChatMessages.AddRange(
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = Other, Content = "1", IsRead = false },
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = Me, Content = "mine", IsRead = false });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        await svc.MarkAsReadAsync(Me, conv.Id);

        (await db.ChatMessages.CountAsync(m => m.SenderId == Other && m.IsRead)).ShouldBe(1);
        (await db.ChatMessages.CountAsync(m => m.SenderId == Me && !m.IsRead)).ShouldBe(1);
    }
}
