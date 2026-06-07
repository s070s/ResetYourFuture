using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Hubs;
using ResetYourFuture.Web.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class ChatHubTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    private sealed class Harness
    {
        public required ChatHub Hub;
        public required ISingleClientProxy Caller;
        public required IClientProxy Group;
        public required HubCallerContext Context;
        public required IGroupManager Groups;
        public required UserManager<ApplicationUser> Um;
        public required ISubscriptionService Subs;
    }

    private static ApplicationUser AppUser( string id, bool enabled = true ) =>
        new() { Id = id, UserName = id, Email = $"{id}@x.com", FirstName = "F", LastName = "L", IsEnabled = enabled };

    private static Harness BuildHub( ApplicationDbContext db, string? userId, bool isAdmin = false )
    {
        var caller = Substitute.For<ISingleClientProxy>();
        var group = Substitute.For<IClientProxy>();
        var context = Substitute.For<HubCallerContext>();
        context.UserIdentifier.Returns( userId );
        context.ConnectionId.Returns( "conn-1" );
        var claims = isAdmin ? new[] { new Claim( ClaimTypes.Role, "Admin" ) } : [];
        context.User.Returns( new ClaimsPrincipal( new ClaimsIdentity( claims, "test" ) ) );

        var clients = Substitute.For<IHubCallerClients>();
        clients.Caller.Returns( caller );
        clients.Group( Arg.Any<string>() ).Returns( group );

        var groups = Substitute.For<IGroupManager>();
        var um = IdentityMocks.MockUserManager();
        var subs = Substitute.For<ISubscriptionService>();

        var hub = new ChatHub( db, um, subs, NullLogger<ChatHub>.Instance )
        {
            Clients = clients,
            Context = context,
            Groups = groups
        };

        return new Harness { Hub = hub, Caller = caller, Group = group, Context = context, Groups = groups, Um = um, Subs = subs };
    }

    private static ChatConversation Conversation( string creator, string participant ) =>
        new() { Id = Guid.NewGuid(), CreatorId = creator, ParticipantId = participant };

    private static UserSubscriptionStatusDto Status( bool priority ) =>
        new( SubscriptionTierEnum.Free, "n", DateTime.UtcNow, null, true, new PlanFeaturesDto { PrioritySupport = priority } );

    [Fact]
    public async Task OnConnected_DisabledUser_Aborts()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub( db, Me );
        h.Um.FindByIdAsync( Me ).Returns( AppUser( Me, enabled: false ) );

        await h.Hub.OnConnectedAsync();

        h.Context.Received().Abort();
        await h.Groups.DidNotReceive().AddToGroupAsync( Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task OnConnected_EnabledUser_JoinsGroup()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub( db, Me );
        h.Um.FindByIdAsync( Me ).Returns( AppUser( Me ) );

        await h.Hub.OnConnectedAsync();

        await h.Groups.Received().AddToGroupAsync( "conn-1", $"user_{Me}", Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task SendMessage_EmptyContent_NoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub( db, Me, isAdmin: true );

        await h.Hub.SendMessage( Guid.NewGuid(), "   " );

        ( await db.ChatMessages.CountAsync() ).ShouldBe( 0 );
    }

    [Fact]
    public async Task SendMessage_TooLong_SendsChatErrorToCaller()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub( db, Me, isAdmin: true );

        await h.Hub.SendMessage( Guid.NewGuid(), new string( 'x', 4001 ) );

        await h.Caller.Received().SendCoreAsync( "ChatError", Arg.Any<object?[]>(), Arg.Any<CancellationToken>() );
        ( await db.ChatMessages.CountAsync() ).ShouldBe( 0 );
    }

    [Fact]
    public async Task SendMessage_FreeNonAdmin_SendsChatError()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub( db, Me, isAdmin: false );
        h.Subs.GetUserStatusAsync( Me, Arg.Any<CancellationToken>() ).Returns( Status( priority: false ) );

        await h.Hub.SendMessage( Guid.NewGuid(), "hello" );

        await h.Caller.Received().SendCoreAsync( "ChatError", Arg.Any<object?[]>(), Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task SendMessage_ConversationMissing_NoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub( db, Me, isAdmin: true );

        await h.Hub.SendMessage( Guid.NewGuid(), "hello" );

        ( await db.ChatMessages.CountAsync() ).ShouldBe( 0 );
    }

    [Fact]
    public async Task SendMessage_NotParticipant_NoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation( "x", "y" );
        db.ChatConversations.Add( conv );
        await db.SaveChangesAsync();
        var h = BuildHub( db, Me, isAdmin: true );

        await h.Hub.SendMessage( conv.Id, "hello" );

        ( await db.ChatMessages.CountAsync() ).ShouldBe( 0 );
    }

    [Fact]
    public async Task SendMessage_SenderDisabled_Aborts()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation( Me, Other );
        db.ChatConversations.Add( conv );
        await db.SaveChangesAsync();
        var h = BuildHub( db, Me, isAdmin: true );
        h.Um.FindByIdAsync( Me ).Returns( AppUser( Me, enabled: false ) );

        await h.Hub.SendMessage( conv.Id, "hello" );

        h.Context.Received().Abort();
    }

    [Fact]
    public async Task SendMessage_Valid_PersistsAndBroadcasts()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var conv = Conversation( Me, Other );
        db.ChatConversations.Add( conv );
        await db.SaveChangesAsync();
        var h = BuildHub( db, Me, isAdmin: true );
        h.Um.FindByIdAsync( Me ).Returns( AppUser( Me ) );
        h.Um.GetRolesAsync( Arg.Any<ApplicationUser>() ).Returns( new List<string> { "Admin" } );

        await h.Hub.SendMessage( conv.Id, "hello world" );

        ( await db.ChatMessages.CountAsync() ).ShouldBe( 1 );
        ( await db.ChatConversations.FirstAsync() ).LastMessageContent.ShouldBe( "hello world" );
        await h.Group.Received().SendCoreAsync( "ReceiveMessage", Arg.Any<object?[]>(), Arg.Any<CancellationToken>() );
        await h.Group.Received().SendCoreAsync( "ChatNotification", Arg.Any<object?[]>(), Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task MarkAsRead_FlipsOthersUnreadMessages()
    {
        // MarkAsRead uses ExecuteUpdateAsync — unsupported on InMemory, so use SQLite.
        await using var db = DbContextFactory.CreateSqlite();
        // SQLite enforces FK constraints (InMemory does not) — the referenced users must exist.
        db.Users.AddRange( AppUser( Me ), AppUser( Other ) );
        var conv = Conversation( Me, Other );
        db.ChatConversations.Add( conv );
        db.ChatMessages.AddRange(
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = Other, Content = "1", IsRead = false },
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = conv.Id, SenderId = Me, Content = "mine", IsRead = false } );
        await db.SaveChangesAsync();
        var h = BuildHub( db, Me, isAdmin: true );

        await h.Hub.MarkAsRead( conv.Id );

        ( await db.ChatMessages.CountAsync( m => m.SenderId == Other && m.IsRead ) ).ShouldBe( 1 );
        ( await db.ChatMessages.CountAsync( m => m.SenderId == Me && !m.IsRead ) ).ShouldBe( 1 );
    }
}
