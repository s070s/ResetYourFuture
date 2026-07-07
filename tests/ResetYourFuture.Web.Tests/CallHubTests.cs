using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.Hubs;
using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class CallHubTests
{
    private const string Initiator = "user-a";
    private const string Callee = "user-b";

    /// <summary>
    /// One simulated hub connection. Multiple harnesses can share the same <see cref="CallRegistry"/>
    /// and <see cref="ApplicationDbContext"/> (via <see cref="BuildHub"/>'s registry parameter) to
    /// model two different users' connections to the same call, the way two real clients would.
    /// </summary>
    private sealed class Harness
    {
        public required CallHub Hub;
        public required CallRegistry Registry;
        public required HubCallerContext Context;
        public required IGroupManager Groups;
        public required ISingleClientProxy Caller;
        public required Dictionary<string, IClientProxy> CallGroups;
        public required Dictionary<string, IClientProxy> ChatGroups;
        public required UserManager<ApplicationUser> Um;
        public required ISubscriptionService Subs;
    }

    private static ApplicationUser AppUser(string id, bool enabled = true) =>
        new() { Id = id, UserName = id, Email = $"{id}@x.com", FirstName = "F", LastName = "L", IsEnabled = enabled };

    private static IClientProxy ResolveGroupProxy(Dictionary<string, IClientProxy> groups, string name)
    {
        if (!groups.TryGetValue(name, out var proxy))
        {
            proxy = Substitute.For<IClientProxy>();
            groups[name] = proxy;
        }
        return proxy;
    }

    /// <summary>
    /// Builds a CallHub wired to a real CallEventService/CallQueryService (backed by the given
    /// InMemory db) so tests exercise actual persistence logic, not mocks of it — only the SignalR
    /// plumbing (Context/Clients/Groups) and UserManager/subscription checks are substituted,
    /// mirroring ChatHubTests' approach. Pass an existing <paramref name="registry"/> to simulate a
    /// second connection (e.g. the callee) joining the same call as an earlier harness.
    /// </summary>
    private static Harness BuildHub(
        ApplicationDbContext db,
        string? userId,
        bool isAdmin = false,
        string connectionId = "conn-1",
        CallRegistry? registry = null)
    {
        var caller = Substitute.For<ISingleClientProxy>();
        var context = Substitute.For<HubCallerContext>();
        context.UserIdentifier.Returns(userId);
        context.ConnectionId.Returns(connectionId);
        var claims = isAdmin ? new[] { new Claim(ClaimTypes.Role, "Admin") } : [];
        context.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));

        var callGroups = new Dictionary<string, IClientProxy>();
        var clients = Substitute.For<IHubCallerClients>();
        clients.Caller.Returns(caller);
        clients.Group(Arg.Any<string>()).Returns(ci => ResolveGroupProxy(callGroups, (string)ci[0]));

        var groups = Substitute.For<IGroupManager>();
        var um = IdentityMocks.MockUserManager();
        var subs = Substitute.For<ISubscriptionService>();

        registry ??= new CallRegistry();
        var callEventService = new CallEventService(db, um);
        var callQueryService = new CallQueryService(db, um, subs);
        var options = Options.Create(new WebRtcOptions { RingTimeoutSeconds = 45, MaxParticipants = 6 });

        var chatGroups = new Dictionary<string, IClientProxy>();
        var chatClients = Substitute.For<IHubClients>();
        chatClients.Group(Arg.Any<string>()).Returns(ci => ResolveGroupProxy(chatGroups, (string)ci[0]));
        var chatHubContext = Substitute.For<IHubContext<ChatHub>>();
        chatHubContext.Clients.Returns(chatClients);

        var hub = new CallHub(callEventService, callQueryService, registry, um, options, chatHubContext, NullLogger<CallHub>.Instance)
        {
            Clients = clients,
            Context = context,
            Groups = groups
        };

        return new Harness
        {
            Hub = hub,
            Registry = registry,
            Context = context,
            Groups = groups,
            Caller = caller,
            CallGroups = callGroups,
            ChatGroups = chatGroups,
            Um = um,
            Subs = subs
        };
    }

    private static UserSubscriptionStatusDto Status(bool priority) =>
        new(SubscriptionTier.Free, "n", DateTime.UtcNow, null, true, new PlanFeaturesDto { PrioritySupport = priority });

    private static ChatConversation Conversation(string creator, string participant) =>
        new() { Id = Guid.NewGuid(), CreatorId = creator, ParticipantId = participant };

    // ---- OnConnected ----------------------------------------------------------

    [Fact]
    public async Task OnConnected_DisabledUser_Aborts()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub(db, Initiator);
        h.Um.FindByIdAsync(Initiator).Returns(AppUser(Initiator, enabled: false));

        await h.Hub.OnConnectedAsync();

        h.Context.Received().Abort();
        await h.Groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- StartCall access / availability --------------------------------------

    [Fact]
    public async Task StartCall_NoSubscription_ReturnsNoAccess()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub(db, Initiator, isAdmin: false);
        h.Subs.GetUserStatusAsync(Initiator, Arg.Any<CancellationToken>()).Returns(Status(priority: false));

        var result = await h.Hub.StartCall([Callee], null);

        result.StartCallStatus.ShouldBe(StartCallStatus.NoAccess);
    }

    [Fact]
    public async Task StartCall_Admin_BypassesSubscriptionCheck_AndRings()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub(db, Initiator, isAdmin: true);
        h.Registry.AddUserConnection(Callee);

        var result = await h.Hub.StartCall([Callee], null);

        result.StartCallStatus.ShouldBe(StartCallStatus.Ringing);
        result.CallId.ShouldNotBeNull();
    }

    [Fact]
    public async Task StartCall_OfflineCallee_OneToOne_RecordsMissedChatMessage()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(AppUser(Initiator), AppUser(Callee));
        var conv = Conversation(Initiator, Callee);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var h = BuildHub(db, Initiator, isAdmin: true);
        h.Um.FindByIdAsync(Initiator).Returns(AppUser(Initiator));
        h.Um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());
        // Callee is never added to the registry, so it's offline.

        var result = await h.Hub.StartCall([Callee], conv.Id);

        result.StartCallStatus.ShouldBe(StartCallStatus.AllUnavailable);
        (await db.CallSessions.CountAsync()).ShouldBe(1);
        var session = await db.CallSessions.FirstAsync();
        session.EndReason.ShouldBe(CallEndReason.Missed);
        var message = await db.ChatMessages.FirstAsync();
        message.CallEvent.ShouldBe(CallEventKind.Missed);

        h.ChatGroups.ShouldContainKey($"user_{Initiator}");
        h.ChatGroups.ShouldContainKey($"user_{Callee}");
        await h.ChatGroups[$"user_{Initiator}"].Received().SendCoreAsync("ReceiveMessage", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await h.ChatGroups[$"user_{Callee}"].Received().SendCoreAsync("ReceiveMessage", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartCall_CallerAlreadyBusy_ReturnsCallerBusy()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub(db, Initiator, isAdmin: true);
        h.Registry.AddUserConnection(Callee);
        (await h.Hub.StartCall([Callee], null)).StartCallStatus.ShouldBe(StartCallStatus.Ringing);

        // Same initiator tries to start a second call while the first is still active.
        h.Registry.AddUserConnection("user-c");
        var second = await h.Hub.StartCall(["user-c"], null);

        second.StartCallStatus.ShouldBe(StartCallStatus.CallerBusy);
    }

    // ---- AcceptCall -------------------------------------------------------------

    [Fact]
    public async Task AcceptCall_OneToOne_JoinsGroupAndPersistsStartedEvent()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(AppUser(Initiator), AppUser(Callee));
        var conv = Conversation(Initiator, Callee);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var caller = BuildHub(db, Initiator, isAdmin: true, connectionId: "conn-initiator");
        caller.Um.FindByIdAsync(Initiator).Returns(AppUser(Initiator));
        caller.Um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());
        caller.Registry.AddUserConnection(Callee);
        var start = await caller.Hub.StartCall([Callee], conv.Id);
        var callId = start.CallId!.Value;

        var callee = BuildHub(db, Callee, isAdmin: true, connectionId: "conn-callee", registry: caller.Registry);

        var join = await callee.Hub.AcceptCall(callId);

        join.ShouldNotBeNull();
        await callee.Groups.Received().AddToGroupAsync("conn-callee", $"call_{callId}", Arg.Any<CancellationToken>());
        var participant = await db.CallParticipants.FirstAsync(p => p.CallSessionId == callId && p.UserId == Callee);
        participant.Status.ShouldBe(CallParticipantStatus.Joined);
        var session = await db.CallSessions.FirstAsync(s => s.Id == callId);
        session.ConnectedAt.ShouldNotBeNull();
        var chatMessage = await db.ChatMessages.FirstAsync();
        chatMessage.CallEvent.ShouldBe(CallEventKind.Started);
        await callee.CallGroups[$"call_{callId}"].Received().SendCoreAsync("ParticipantJoined", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    // ---- DeclineCall --------------------------------------------------------------

    [Fact]
    public async Task DeclineCall_BeforeConnecting_BroadcastsDeclinedAndEndsAsMissed()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var caller = BuildHub(db, Initiator, isAdmin: true, connectionId: "conn-initiator");
        caller.Registry.AddUserConnection(Callee);
        var start = await caller.Hub.StartCall([Callee], null);
        var callId = start.CallId!.Value;

        var callee = BuildHub(db, Callee, isAdmin: true, connectionId: "conn-callee", registry: caller.Registry);

        await callee.Hub.DeclineCall(callId);

        await callee.CallGroups[$"call_{callId}"].Received().SendCoreAsync("CallDeclined", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        var session = await db.CallSessions.FirstAsync(s => s.Id == callId);
        session.EndedAt.ShouldNotBeNull();
        // The callee never joined (declined the initial ring), so the call never "connected" —
        // per CallHub.ForceEndCall this ends as Missed, matching cancel/timeout's identical
        // "missed call" chat event (see VIDEO_CALL_PLAN.md edge cases).
        session.EndReason.ShouldBe(CallEndReason.Missed);
    }

    // ---- Signaling relay security ------------------------------------------------

    [Fact]
    public async Task SendOffer_TargetNotCallMember_RefusedAndNotRelayed()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub(db, Initiator, isAdmin: true, connectionId: "conn-initiator");
        h.Registry.AddUserConnection(Callee);
        var start = await h.Hub.StartCall([Callee], null);
        var callId = start.CallId!.Value;

        await h.Hub.SendOffer(callId, "conn-not-a-member", new { type = "offer", sdp = "x" });

        await h.Caller.Received().SendCoreAsync("CallError", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        h.CallGroups.ContainsKey("conn-not-a-member").ShouldBeFalse();
    }

    // ---- Media state -------------------------------------------------------------

    [Fact]
    public async Task UpdateMediaState_BroadcastsToCallGroup()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub(db, Initiator, isAdmin: true, connectionId: "conn-initiator");
        h.Registry.AddUserConnection(Callee);
        var start = await h.Hub.StartCall([Callee], null);
        var callId = start.CallId!.Value;

        await h.Hub.UpdateMediaState(callId, new MediaStateDto(false, true, false));

        await h.CallGroups[$"call_{callId}"].Received().SendCoreAsync(
            "ParticipantMediaChanged",
            Arg.Is<object?[]>(a => a.Length == 3 && (Guid)a[0]! == callId && (string)a[1]! == Initiator),
            Arg.Any<CancellationToken>());
    }

    // ---- LeaveCall / end-of-call -------------------------------------------------

    [Fact]
    public async Task LeaveCall_OneToOne_EndsSessionAndPersistsDuration()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(AppUser(Initiator), AppUser(Callee));
        var conv = Conversation(Initiator, Callee);
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync();
        var caller = BuildHub(db, Initiator, isAdmin: true, connectionId: "conn-initiator");
        caller.Um.FindByIdAsync(Initiator).Returns(AppUser(Initiator));
        caller.Um.FindByIdAsync(Callee).Returns(AppUser(Callee));
        caller.Um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(new List<string>());
        caller.Registry.AddUserConnection(Callee);
        var start = await caller.Hub.StartCall([Callee], conv.Id);
        var callId = start.CallId!.Value;

        var callee = BuildHub(db, Callee, isAdmin: true, connectionId: "conn-callee", registry: caller.Registry);
        await callee.Hub.AcceptCall(callId);

        var session = await db.CallSessions.FirstAsync(s => s.Id == callId);
        session.ConnectedAt = session.ConnectedAt!.Value.AddSeconds(-42);
        await db.SaveChangesAsync();

        // In a 1:1 call, either party leaving immediately ends it — only one person would remain,
        // which ShouldEndCall (joined < 2, no pending invites) treats as call-over.
        await caller.Hub.LeaveCall(callId);

        var ended = await db.CallSessions.FirstAsync(s => s.Id == callId);
        ended.EndedAt.ShouldNotBeNull();
        ended.EndReason.ShouldBe(CallEndReason.Completed);
        var durationSeconds = (ended.EndedAt!.Value - ended.ConnectedAt!.Value).TotalSeconds;
        durationSeconds.ShouldBeInRange(41, 43);

        var endedMessage = await db.ChatMessages.FirstAsync(m => m.CallEvent == CallEventKind.Ended);
        endedMessage.Content.ShouldStartWith("Video call ended");
    }

    // ---- InviteToCall participant cap --------------------------------------------

    [Fact]
    public async Task InviteToCall_AtCapacity_RejectsSeventhParticipant()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var h = BuildHub(db, Initiator, isAdmin: true, connectionId: "conn-initiator");
        var invitees = Enumerable.Range(1, 5).Select(i => $"invitee-{i}").ToList();
        foreach (var id in invitees) h.Registry.AddUserConnection(id);
        var start = await h.Hub.StartCall(invitees, null);
        var callId = start.CallId!.Value;
        // 6 participants total now (initiator + 5 invitees) — at MaxParticipants cap.

        h.Registry.AddUserConnection("invitee-6");
        await h.Hub.InviteToCall(callId, "invitee-6");

        await h.Caller.Received().SendCoreAsync("CallError", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        h.Registry.IsUserBusy("invitee-6").ShouldBeFalse();
    }
}
