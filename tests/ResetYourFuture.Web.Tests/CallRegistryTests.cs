using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class CallRegistryTests
{
    private const string Initiator = "user-initiator";
    private const string Callee1 = "user-callee-1";
    private const string Callee2 = "user-callee-2";
    private const string InitiatorConn = "conn-initiator";

    private static CallRegistry NewRegistry() => new();

    // --- TryCreateCall ---

    [Fact]
    public void TryCreateCall_NewCall_Succeeds()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();

        var result = registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        result.ShouldBeTrue();
        registry.CallExists(callId).ShouldBeTrue();
        registry.ParticipantCount(callId).ShouldBe(2);
    }

    [Fact]
    public void TryCreateCall_DuplicateCallId_Fails()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        var result = registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee2], isGroup: false);

        result.ShouldBeFalse();
        registry.ParticipantCount(callId).ShouldBe(2);
    }

    [Fact]
    public void TryCreateCall_InitiatorStartsJoined_CalleesStartInvited()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        var call = registry.GetCall(callId)!;
        call.Participants[Initiator].State.ShouldBe(RegistryParticipantState.Joined);
        call.Participants[Callee1].State.ShouldBe(RegistryParticipantState.Invited);
    }

    // --- IsUserBusy ---

    [Fact]
    public void IsUserBusy_NotInAnyCall_ReturnsFalse()
    {
        var registry = NewRegistry();

        registry.IsUserBusy(Initiator).ShouldBeFalse();
    }

    [Fact]
    public void IsUserBusy_InitiatorOfActiveCall_ReturnsTrue()
    {
        var registry = NewRegistry();
        registry.TryCreateCall(Guid.NewGuid(), Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.IsUserBusy(Initiator).ShouldBeTrue();
    }

    [Fact]
    public void IsUserBusy_InvitedCallee_ReturnsTrue()
    {
        var registry = NewRegistry();
        registry.TryCreateCall(Guid.NewGuid(), Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.IsUserBusy(Callee1).ShouldBeTrue();
    }

    [Fact]
    public void IsUserBusy_AfterDecline_ReturnsFalse()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.TryDecline(callId, Callee1);

        registry.IsUserBusy(Callee1).ShouldBeFalse();
    }

    // --- TryAddInvite ---

    [Fact]
    public void TryAddInvite_NewUser_Succeeds()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: true);

        var result = registry.TryAddInvite(callId, Callee2, maxParticipants: 6);

        result.ShouldBeTrue();
        registry.ParticipantCount(callId).ShouldBe(3);
    }

    [Fact]
    public void TryAddInvite_CallDoesNotExist_Fails()
    {
        var registry = NewRegistry();

        registry.TryAddInvite(Guid.NewGuid(), Callee1, maxParticipants: 6).ShouldBeFalse();
    }

    [Fact]
    public void TryAddInvite_UserAlreadyMember_Fails()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: true);

        registry.TryAddInvite(callId, Callee1, maxParticipants: 6).ShouldBeFalse();
    }

    [Fact]
    public void TryAddInvite_AtCapacity_Fails()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        // Initiator + 5 callees = 6 (the cap).
        var callees = Enumerable.Range(1, 5).Select(i => $"user-{i}").ToList();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, callees, isGroup: true);

        var result = registry.TryAddInvite(callId, "user-overflow", maxParticipants: 6);

        result.ShouldBeFalse();
        registry.ParticipantCount(callId).ShouldBe(6);
    }

    // --- TryAccept ---

    [Fact]
    public void TryAccept_PendingInvite_Succeeds()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        var result = registry.TryAccept(callId, Callee1, "conn-callee-1");

        result.ShouldBeTrue();
        var call = registry.GetCall(callId)!;
        call.Participants[Callee1].State.ShouldBe(RegistryParticipantState.Joined);
        call.Participants[Callee1].ConnectionId.ShouldBe("conn-callee-1");
    }

    [Fact]
    public void TryAccept_UnknownCall_Fails()
    {
        var registry = NewRegistry();

        registry.TryAccept(Guid.NewGuid(), Callee1, "conn-x").ShouldBeFalse();
    }

    [Fact]
    public void TryAccept_UserNotInvited_Fails()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.TryAccept(callId, "user-random", "conn-x").ShouldBeFalse();
    }

    [Fact]
    public void TryAccept_BringsJoinedCountToTwo_MarksHasConnected()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.TryAccept(callId, Callee1, "conn-callee-1");

        registry.GetCall(callId)!.HasConnected.ShouldBeTrue();
    }

    [Fact]
    public void HasConnected_StaysTrueAfterParticipantLeaves()
    {
        // Sticky flag: once a call has genuinely connected, later leaves must not make
        // end-of-call logic mistake it for a call that was never answered (Missed).
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-callee-1");

        registry.TryLeave(callId, Callee1);

        registry.GetCall(callId)!.HasConnected.ShouldBeTrue();
    }

    [Fact]
    public void HasConnected_NeverAccepted_StaysFalse()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.GetCall(callId)!.HasConnected.ShouldBeFalse();
    }

    [Fact]
    public void TryAccept_DoubleAccept_SecondCallSucceedsAndReKeys()
    {
        // Two tabs both accept — first wins in the sense that the hub layer dismisses other
        // tabs via IncomingCallHandled, but the registry itself just re-keys to whichever
        // connection calls TryAccept last; it doesn't reject a second accept outright.
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.TryAccept(callId, Callee1, "conn-tab-1").ShouldBeTrue();
        registry.TryAccept(callId, Callee1, "conn-tab-2").ShouldBeTrue();

        registry.GetCall(callId)!.Participants[Callee1].ConnectionId.ShouldBe("conn-tab-2");
    }

    // --- TryDecline / TryLeave ---

    [Fact]
    public void TryDecline_RemovesParticipant()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.TryDecline(callId, Callee1).ShouldBeTrue();

        registry.ParticipantCount(callId).ShouldBe(1);
    }

    [Fact]
    public void TryDecline_UnknownCall_Fails()
    {
        var registry = NewRegistry();

        registry.TryDecline(Guid.NewGuid(), Callee1).ShouldBeFalse();
    }

    [Fact]
    public void TryLeave_JoinedParticipant_Removed()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-callee-1");

        registry.TryLeave(callId, Callee1).ShouldBeTrue();

        registry.ParticipantCount(callId).ShouldBe(1);
    }

    // --- TryUpdateConnection (rejoin re-keying) ---

    [Fact]
    public void TryUpdateConnection_ExistingMember_ReKeysAndMarksJoined()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-old");
        registry.HandleDisconnect("conn-old");

        var result = registry.TryUpdateConnection(callId, Callee1, "conn-new");

        result.ShouldBeTrue();
        var participant = registry.GetCall(callId)!.Participants[Callee1];
        participant.ConnectionId.ShouldBe("conn-new");
        participant.State.ShouldBe(RegistryParticipantState.Joined);
        participant.DisconnectedAt.ShouldBeNull();
    }

    [Fact]
    public void TryUpdateConnection_CallNoLongerExists_Fails()
    {
        var registry = NewRegistry();

        registry.TryUpdateConnection(Guid.NewGuid(), Callee1, "conn-new").ShouldBeFalse();
    }

    [Fact]
    public void TryUpdateConnection_UserNotInCall_Fails()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.TryUpdateConnection(callId, "user-stranger", "conn-new").ShouldBeFalse();
    }

    // --- HandleDisconnect / TakeExpiredDisconnects ---

    [Fact]
    public void HandleDisconnect_JoinedParticipant_MarksDisconnected()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-callee-1");

        var affected = registry.HandleDisconnect("conn-callee-1");

        affected.ShouldContain(callId);
        registry.GetCall(callId)!.Participants[Callee1].State.ShouldBe(RegistryParticipantState.Disconnected);
    }

    [Fact]
    public void HandleDisconnect_UnknownConnection_ReturnsEmpty()
    {
        var registry = NewRegistry();
        registry.TryCreateCall(Guid.NewGuid(), Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.HandleDisconnect("conn-unknown").ShouldBeEmpty();
    }

    [Fact]
    public void TakeExpiredDisconnects_WithinGrace_NotReturned()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-callee-1");
        registry.HandleDisconnect("conn-callee-1");

        // Grace of 9999s means "just disconnected" is nowhere near the cutoff.
        var expired = registry.TakeExpiredDisconnects(graceSeconds: 9999);

        expired.ShouldBeEmpty();
        registry.ParticipantCount(callId).ShouldBe(2);
    }

    [Fact]
    public void TakeExpiredDisconnects_PastGrace_RemovesParticipant()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-callee-1");
        registry.HandleDisconnect("conn-callee-1");

        // graceSeconds <= 0 means "expired immediately" — boundary: cutoff is now or later than DisconnectedAt.
        var expired = registry.TakeExpiredDisconnects(graceSeconds: 0);

        expired.ShouldContain((callId, Callee1));
        registry.ParticipantCount(callId).ShouldBe(1);
    }

    // --- TakeExpiredInvites (expiry boundary) ---

    [Fact]
    public void TakeExpiredInvites_WithinTimeout_NotReturned()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        var expired = registry.TakeExpiredInvites(ringTimeoutSeconds: 9999);

        expired.ShouldBeEmpty();
        registry.ParticipantCount(callId).ShouldBe(2);
    }

    [Fact]
    public void TakeExpiredInvites_PastTimeout_RemovesInvite()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        var expired = registry.TakeExpiredInvites(ringTimeoutSeconds: 0);

        expired.ShouldContain((callId, Callee1));
        registry.ParticipantCount(callId).ShouldBe(1);
    }

    [Fact]
    public void TakeExpiredInvites_JoinedParticipant_NeverExpires()
    {
        // Joined participants have no InvitedAt-based timeout — only pending Invited entries expire.
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-callee-1");

        var expired = registry.TakeExpiredInvites(ringTimeoutSeconds: 0);

        expired.ShouldBeEmpty();
        registry.ParticipantCount(callId).ShouldBe(2);
    }

    // --- ShouldEndCall (pure end-condition check) ---

    [Fact]
    public void ShouldEndCall_OnlyInitiatorJoined_PendingInviteRinging_ReturnsFalse()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.ShouldEndCall(callId).ShouldBeFalse();
    }

    [Fact]
    public void ShouldEndCall_AllInvitesGone_OnlyOneJoined_ReturnsTrue()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryDecline(callId, Callee1);

        registry.ShouldEndCall(callId).ShouldBeTrue();
    }

    [Fact]
    public void ShouldEndCall_TwoJoined_ReturnsFalse()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-callee-1");

        registry.ShouldEndCall(callId).ShouldBeFalse();
    }

    [Fact]
    public void ShouldEndCall_AfterOneOfTwoLeaves_ReturnsTrue()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);
        registry.TryAccept(callId, Callee1, "conn-callee-1");
        registry.TryLeave(callId, Callee1);

        registry.ShouldEndCall(callId).ShouldBeTrue();
    }

    [Fact]
    public void ShouldEndCall_UnknownCall_ReturnsFalse()
    {
        var registry = NewRegistry();

        registry.ShouldEndCall(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void ShouldEndCall_GroupCallWithThreeJoinedAndOneInviteLeft_ReturnsFalse()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1, Callee2], isGroup: true);
        registry.TryAccept(callId, Callee1, "conn-callee-1");

        registry.ShouldEndCall(callId).ShouldBeFalse();
    }

    // --- IsMemberConnection ---

    [Fact]
    public void IsMemberConnection_JoinedParticipantConnection_ReturnsTrue()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.IsMemberConnection(callId, InitiatorConn).ShouldBeTrue();
    }

    [Fact]
    public void IsMemberConnection_InvitedButNotJoined_ReturnsFalse()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.IsMemberConnection(callId, "conn-callee-1").ShouldBeFalse();
    }

    [Fact]
    public void IsMemberConnection_UnknownConnection_ReturnsFalse()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.IsMemberConnection(callId, "conn-random").ShouldBeFalse();
    }

    // --- GetJoinedParticipants ---

    [Fact]
    public void GetJoinedParticipants_ExcludesInvitedAndExcludedUser()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1, Callee2], isGroup: true);
        registry.TryAccept(callId, Callee1, "conn-callee-1");

        var participants = registry.GetJoinedParticipants(callId, excludingUserId: Callee1);

        participants.Count.ShouldBe(1);
        participants[0].UserId.ShouldBe(Initiator);
    }

    // --- Presence (online/offline) ---

    [Fact]
    public void IsUserOnline_AfterAddConnection_ReturnsTrue()
    {
        var registry = NewRegistry();

        registry.AddUserConnection(Callee1);

        registry.IsUserOnline(Callee1).ShouldBeTrue();
    }

    [Fact]
    public void IsUserOnline_AfterRemoveConnection_ReturnsFalse()
    {
        var registry = NewRegistry();
        registry.AddUserConnection(Callee1);

        registry.RemoveUserConnection(Callee1);

        registry.IsUserOnline(Callee1).ShouldBeFalse();
    }

    [Fact]
    public void IsUserOnline_MultipleTabs_StaysOnlineUntilLastRemoved()
    {
        var registry = NewRegistry();
        registry.AddUserConnection(Callee1);
        registry.AddUserConnection(Callee1);

        registry.RemoveUserConnection(Callee1);
        registry.IsUserOnline(Callee1).ShouldBeTrue();

        registry.RemoveUserConnection(Callee1);
        registry.IsUserOnline(Callee1).ShouldBeFalse();
    }

    [Fact]
    public void AddUserConnection_FirstConnection_ReturnsTrue()
    {
        var registry = NewRegistry();

        bool wentOnline = registry.AddUserConnection(Callee1);

        wentOnline.ShouldBeTrue();
    }

    [Fact]
    public void AddUserConnection_SecondConnection_ReturnsFalse()
    {
        var registry = NewRegistry();
        registry.AddUserConnection(Callee1);

        bool wentOnline = registry.AddUserConnection(Callee1);

        wentOnline.ShouldBeFalse();
    }

    [Fact]
    public void RemoveUserConnection_LastConnection_ReturnsTrue()
    {
        var registry = NewRegistry();
        registry.AddUserConnection(Callee1);

        bool wentOffline = registry.RemoveUserConnection(Callee1);

        wentOffline.ShouldBeTrue();
    }

    [Fact]
    public void RemoveUserConnection_WithRemainingConnection_ReturnsFalse()
    {
        var registry = NewRegistry();
        registry.AddUserConnection(Callee1);
        registry.AddUserConnection(Callee1);

        bool wentOffline = registry.RemoveUserConnection(Callee1);

        wentOffline.ShouldBeFalse();
    }

    [Fact]
    public void RemoveUserConnection_UnknownUser_ReturnsFalse()
    {
        var registry = NewRegistry();

        bool wentOffline = registry.RemoveUserConnection(Callee1);

        wentOffline.ShouldBeFalse();
    }

    [Fact]
    public void GetOnlineUserIds_ReturnsCurrentlyOnlineUsers()
    {
        var registry = NewRegistry();
        registry.AddUserConnection(Callee1);
        registry.AddUserConnection(Callee2);
        registry.RemoveUserConnection(Callee2);

        var online = registry.GetOnlineUserIds();

        online.ShouldBe([Callee1]);
    }

    // --- RemoveCall ---

    [Fact]
    public void RemoveCall_MakesCallNoLongerExist()
    {
        var registry = NewRegistry();
        var callId = Guid.NewGuid();
        registry.TryCreateCall(callId, Initiator, InitiatorConn, null, [Callee1], isGroup: false);

        registry.RemoveCall(callId);

        registry.CallExists(callId).ShouldBeFalse();
    }
}
