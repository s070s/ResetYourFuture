using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
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

    private static (ChatCommandService svc, UserManager<ApplicationUser> um) NewService(ApplicationDbContext db)
    {
        var um = IdentityMocks.MockUserManager();
        return (new ChatCommandService(db, um), um);
    }

    private static ApplicationUser AppUser(string id, bool enabled = true) =>
        new() { Id = id, UserName = id, Email = $"{id}@x.com", FirstName = "F", LastName = "L", IsEnabled = enabled };

    private static ChatConversation Conversation(string creator, string participant) =>
        new() { Id = Guid.NewGuid(), CreatorId = creator, ParticipantId = participant };

    // ---- SendMessageAsync ------------------------------------------------------

    [Fact]
    public async Task SendMessage_EmptyContent_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);

        var result = await svc.SendMessageAsync(Me, Guid.NewGuid(), "   ");

        result.StatusCode.ShouldBe(400);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_TooLong_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);

        var result = await svc.SendMessageAsync(Me, Guid.NewGuid(), new string('x', 4001));

        result.StatusCode.ShouldBe(400);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_ConversationMissing_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);

        var result = await svc.SendMessageAsync(Me, Guid.NewGuid(), "hello");

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
        var (svc, _) = NewService(db);

        var result = await svc.SendMessageAsync(Me, conv.Id, "hello");

        result.StatusCode.ShouldBe(403);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_SenderDisabled_ReturnsUnauthorized()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation(Me, Other);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var (svc, um) = NewService(db);
        um.FindByIdAsync(Me).Returns(AppUser(Me, enabled: false));

        var result = await svc.SendMessageAsync(Me, conv.Id, "hello");

        result.StatusCode.ShouldBe(401);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_Valid_PersistsAndReturnsRecipientId()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation(Me, Other);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var (svc, um) = NewService(db);
        um.FindByIdAsync(Me).Returns(AppUser(Me));
        um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string> { "Admin" });

        var result = await svc.SendMessageAsync(Me, conv.Id, "hello world");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RecipientId.ShouldBe(Other);
        result.Value.Message.Content.ShouldBe("hello world");
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
        var (svc, _) = NewService(db);

        await svc.MarkAsReadAsync(Me, conv.Id);

        (await db.ChatMessages.CountAsync(m => m.SenderId == Other && m.IsRead)).ShouldBe(1);
        (await db.ChatMessages.CountAsync(m => m.SenderId == Me && !m.IsRead)).ShouldBe(1);
    }
}
