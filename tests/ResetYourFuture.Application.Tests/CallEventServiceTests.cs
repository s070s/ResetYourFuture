using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class CallEventServiceTests
{
    private const string Initiator = "user-a";
    private const string Invitee = "user-b";

    private static (CallEventService svc, UserManager<ApplicationUser> um) NewService(ApplicationDbContext db)
    {
        var um = IdentityMocks.MockUserManager();
        return (new CallEventService(db, um), um);
    }

    private static ApplicationUser AppUser(string id, string first = "F", string last = "L") =>
        new() { Id = id, UserName = id, Email = $"{id}@x.com", FirstName = first, LastName = last };

    private static ChatConversation Conversation(string creator, string participant) =>
        new() { Id = Guid.NewGuid(), CreatorId = creator, ParticipantId = participant };

    // ---- CreateSessionAsync --------------------------------------------------

    [Fact]
    public async Task CreateSession_PersistsSessionWithInitiatorAndInvitees()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);

        var session = await svc.CreateSessionAsync(Initiator, conversationId: null, [Invitee, "user-c"]);

        session.InitiatorId.ShouldBe(Initiator);
        (await db.CallSessions.CountAsync()).ShouldBe(1);
        var participants = await db.CallParticipants.Where(p => p.CallSessionId == session.Id).ToListAsync();
        participants.Count.ShouldBe(3);
        participants.ShouldAllBe(p => p.Status == CallParticipantStatus.Invited);
    }

    [Fact]
    public async Task CreateSession_SetsConversationId_WhenProvided()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);
        var convId = Guid.NewGuid();

        var session = await svc.CreateSessionAsync(Initiator, convId, [Invitee]);

        session.ConversationId.ShouldBe(convId);
    }

    // ---- MarkParticipantAsync -------------------------------------------------

    [Fact]
    public async Task MarkParticipant_Joined_SetsJoinedAtAndSessionConnectedAtOnce()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);
        var session = await svc.CreateSessionAsync(Initiator, null, [Invitee]);

        await svc.MarkParticipantAsync(session.Id, Invitee, CallParticipantStatus.Joined);
        var firstConnectedAt = (await db.CallSessions.FirstAsync(s => s.Id == session.Id)).ConnectedAt;

        firstConnectedAt.ShouldNotBeNull();

        // A second participant joining shouldn't move ConnectedAt.
        await svc.MarkParticipantAsync(session.Id, Initiator, CallParticipantStatus.Joined);
        var secondConnectedAt = (await db.CallSessions.FirstAsync(s => s.Id == session.Id)).ConnectedAt;

        secondConnectedAt.ShouldBe(firstConnectedAt);

        var participant = await db.CallParticipants.FirstAsync(p => p.CallSessionId == session.Id && p.UserId == Invitee);
        participant.Status.ShouldBe(CallParticipantStatus.Joined);
        participant.JoinedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkParticipant_Left_SetsLeftAt()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);
        var session = await svc.CreateSessionAsync(Initiator, null, [Invitee]);

        await svc.MarkParticipantAsync(session.Id, Invitee, CallParticipantStatus.Left);

        var participant = await db.CallParticipants.FirstAsync(p => p.CallSessionId == session.Id && p.UserId == Invitee);
        participant.Status.ShouldBe(CallParticipantStatus.Left);
        participant.LeftAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkParticipant_UnknownParticipant_NoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);
        var session = await svc.CreateSessionAsync(Initiator, null, []);

        await Should.NotThrowAsync(() => svc.MarkParticipantAsync(session.Id, "nobody", CallParticipantStatus.Joined));
    }

    // ---- EndSessionAsync --------------------------------------------------

    [Fact]
    public async Task EndSession_SetsEndedAtAndReason()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);
        var session = await svc.CreateSessionAsync(Initiator, null, [Invitee]);

        await svc.EndSessionAsync(session.Id, CallEndReason.Completed);

        var persisted = await db.CallSessions.FirstAsync(s => s.Id == session.Id);
        persisted.EndedAt.ShouldNotBeNull();
        persisted.EndReason.ShouldBe(CallEndReason.Completed);
    }

    [Fact]
    public async Task EndSession_UnknownSession_NoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);

        await Should.NotThrowAsync(() => svc.EndSessionAsync(Guid.NewGuid(), CallEndReason.Cancelled));
    }

    // ---- RecordChatEventAsync -----------------------------------------------

    [Fact]
    public async Task RecordChatEvent_GroupCall_ReturnsNull_AndWritesNoMessage()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);
        var session = await svc.CreateSessionAsync(Initiator, conversationId: null, [Invitee]);

        var result = await svc.RecordChatEventAsync(session.Id, CallEventKind.Started);

        result.ShouldBeNull();
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RecordChatEvent_OneToOne_Started_WritesMessageAndUpdatesConversationCache()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.Add(AppUser(Initiator, "Caller", "One"));
        var conv = Conversation(Initiator, Invitee);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var (svc, um) = NewService(db);
        um.FindByIdAsync(Initiator).Returns(AppUser(Initiator, "Caller", "One"));
        um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());

        var session = await svc.CreateSessionAsync(Initiator, conv.Id, [Invitee]);

        var dto = await svc.RecordChatEventAsync(session.Id, CallEventKind.Started);

        dto.ShouldNotBeNull();
        dto.Content.ShouldBe("Video call started");
        dto.CallEvent.ShouldBe(CallEventKind.Started);
        dto.CallDurationSeconds.ShouldBeNull();

        var persistedConv = await db.ChatConversations.FirstAsync(c => c.Id == conv.Id);
        persistedConv.LastMessageContent.ShouldBe("Video call started");
        persistedConv.LastMessageAt.ShouldNotBeNull();

        var message = await db.ChatMessages.FirstAsync(m => m.ConversationId == conv.Id);
        message.CallSessionId.ShouldBe(session.Id);
        message.CallEvent.ShouldBe(CallEventKind.Started);
    }

    [Fact]
    public async Task RecordChatEvent_OneToOne_Missed_UsesMissedCallContent()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.Add(AppUser(Initiator));
        var conv = Conversation(Initiator, Invitee);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var (svc, um) = NewService(db);
        um.FindByIdAsync(Initiator).Returns(AppUser(Initiator));
        um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());

        var session = await svc.CreateSessionAsync(Initiator, conv.Id, [Invitee]);

        var dto = await svc.RecordChatEventAsync(session.Id, CallEventKind.Missed);

        dto.ShouldNotBeNull();
        dto.Content.ShouldBe("Missed call");
        dto.CallEvent.ShouldBe(CallEventKind.Missed);
    }

    [Fact]
    public async Task RecordChatEvent_OneToOne_Ended_IncludesFormattedDuration()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.Add(AppUser(Initiator));
        var conv = Conversation(Initiator, Invitee);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var (svc, um) = NewService(db);
        um.FindByIdAsync(Initiator).Returns(AppUser(Initiator));
        um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());

        var session = await svc.CreateSessionAsync(Initiator, conv.Id, [Invitee]);
        await svc.MarkParticipantAsync(session.Id, Invitee, CallParticipantStatus.Joined);

        var persisted = await db.CallSessions.FirstAsync(s => s.Id == session.Id);
        persisted.ConnectedAt = persisted.ConnectedAt!.Value.AddSeconds(-90); // simulate 1:30 elapsed
        await db.SaveChangesAsync();
        await svc.EndSessionAsync(session.Id, CallEndReason.Completed);

        var dto = await svc.RecordChatEventAsync(session.Id, CallEventKind.Ended);

        dto.ShouldNotBeNull();
        dto.Content.ShouldStartWith("Video call ended ·");
        dto.CallDurationSeconds.ShouldNotBeNull();
        dto.CallDurationSeconds!.Value.ShouldBeInRange(89, 91);
    }

    [Fact]
    public async Task RecordChatEvent_UnknownSession_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _) = NewService(db);

        var dto = await svc.RecordChatEventAsync(Guid.NewGuid(), CallEventKind.Started);

        dto.ShouldBeNull();
    }
}
