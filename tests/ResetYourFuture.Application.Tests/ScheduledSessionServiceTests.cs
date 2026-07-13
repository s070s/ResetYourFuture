using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class ScheduledSessionServiceTests
{
    private const string HostId = "host-1";
    private const string StudentId = "student-1";
    private const string OtherStudentId = "student-2";

    private static ScheduledSessionService NewService(ApplicationDbContext db) =>
        new(db, NullLogger<ScheduledSessionService>.Instance);

    private static ApplicationUser AppUser(string id, string? displayName = null) => new()
    {
        Id = id, UserName = id, Email = $"{id}@x.com", FirstName = "First", LastName = "Last", DisplayName = displayName
    };

    private static ScheduledSession NewSession(int minutesFromNow = 60, int maxParticipants = 6, ScheduledSessionStatus status = ScheduledSessionStatus.Scheduled) => new()
    {
        Id = Guid.NewGuid(),
        HostUserId = HostId,
        TitleEn = "Office Hours",
        StartsAtUtc = DateTimeOffset.UtcNow.AddMinutes(minutesFromNow),
        DurationMinutes = 30,
        MaxParticipants = maxParticipants,
        Status = status
    };

    // --- Upcoming list ---

    [Fact]
    public async Task GetUpcomingAsync_ExcludesEndedAndCancelled()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.Add(AppUser(HostId, "Host Name"));
        db.ScheduledSessions.Add(NewSession(status: ScheduledSessionStatus.Ended));
        db.ScheduledSessions.Add(NewSession(status: ScheduledSessionStatus.Cancelled));
        await db.SaveChangesAsync();

        var result = await NewService(db).GetUpcomingAsync(userId: null, lang: "en");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUpcomingAsync_Anonymous_NoOtherParticipantIds()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.Add(AppUser(HostId, "Host Name"));
        db.ScheduledSessions.Add(NewSession());
        await db.SaveChangesAsync();

        var result = await NewService(db).GetUpcomingAsync(userId: null, lang: "en");

        var item = result.ShouldHaveSingleItem();
        item.IsHost.ShouldBeFalse();
        item.IsRegistered.ShouldBeFalse();
        item.OtherParticipantUserIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUpcomingAsync_Host_SeesOtherParticipantIds()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.Add(AppUser(HostId, "Host Name"));
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        db.SessionRegistrations.Add(new SessionRegistration { SessionId = session.Id, UserId = StudentId });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetUpcomingAsync(HostId, lang: "en");

        var item = result.ShouldHaveSingleItem();
        item.IsHost.ShouldBeTrue();
        item.OtherParticipantUserIds.ShouldBe([StudentId]);
    }

    [Fact]
    public async Task GetUpcomingAsync_Registrant_SeesHostAndOtherRegistrantsExcludingSelf()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.Add(AppUser(HostId, "Host Name"));
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        db.SessionRegistrations.Add(new SessionRegistration { SessionId = session.Id, UserId = StudentId });
        db.SessionRegistrations.Add(new SessionRegistration { SessionId = session.Id, UserId = OtherStudentId });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetUpcomingAsync(StudentId, lang: "en");

        var item = result.ShouldHaveSingleItem();
        item.IsRegistered.ShouldBeTrue();
        item.IsHost.ShouldBeFalse();
        item.OtherParticipantUserIds.ShouldBe([HostId, OtherStudentId], ignoreOrder: true);
    }

    // --- Register / Unregister ---

    [Fact]
    public async Task RegisterAsync_Succeeds_ForRegularUser()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync();

        var result = await NewService(db).RegisterAsync(session.Id, StudentId);

        result.IsSuccess.ShouldBeTrue();
        (await db.SessionRegistrations.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task RegisterAsync_Host_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync();

        var result = await NewService(db).RegisterAsync(session.Id, HostId);

        result.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RegisterAsync_AlreadyRegistered_ReturnsConflict()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        db.SessionRegistrations.Add(new SessionRegistration { SessionId = session.Id, UserId = StudentId });
        await db.SaveChangesAsync();

        var result = await NewService(db).RegisterAsync(session.Id, StudentId);

        result.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task RegisterAsync_SessionFull_ReturnsConflict()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession(maxParticipants: 1);
        db.ScheduledSessions.Add(session);
        db.SessionRegistrations.Add(new SessionRegistration { SessionId = session.Id, UserId = OtherStudentId });
        await db.SaveChangesAsync();

        var result = await NewService(db).RegisterAsync(session.Id, StudentId);

        result.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task RegisterAsync_CancelledSession_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession(status: ScheduledSessionStatus.Cancelled);
        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync();

        var result = await NewService(db).RegisterAsync(session.Id, StudentId);

        result.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task UnregisterAsync_NotRegistered_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync();

        var result = await NewService(db).UnregisterAsync(session.Id, StudentId);

        result.StatusCode.ShouldBe(404);
    }

    // --- Link call ---

    [Fact]
    public async Task LinkCallSessionAsync_NonParticipant_ReturnsForbidden()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync();

        var result = await NewService(db).LinkCallSessionAsync(session.Id, StudentId, Guid.NewGuid());

        result.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task LinkCallSessionAsync_FirstWriterWins()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync();
        var firstCallId = Guid.NewGuid();
        var secondCallId = Guid.NewGuid();

        await NewService(db).LinkCallSessionAsync(session.Id, HostId, firstCallId);
        await NewService(db).LinkCallSessionAsync(session.Id, HostId, secondCallId);

        (await db.ScheduledSessions.FindAsync(session.Id))!.CallSessionId.ShouldBe(firstCallId);
    }

    // --- Admin CRUD ---

    [Fact]
    public async Task CreateAsync_ClampsMaxParticipantsTo6()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Users.Add(AppUser(HostId, "Host Name"));
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateAsync(
            HostId, new SaveScheduledSessionRequest("Big Group", null, null, DateTimeOffset.UtcNow.AddHours(1), 30, 99));

        result.MaxParticipants.ShouldBe(6);
    }

    [Fact]
    public async Task CancelAsync_SetsStatusCancelled()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var session = NewSession();
        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync();

        (await NewService(db).CancelAsync(session.Id)).ShouldBeTrue();

        (await db.ScheduledSessions.FindAsync(session.Id))!.Status.ShouldBe(ScheduledSessionStatus.Cancelled);
    }

    [Fact]
    public async Task CancelAsync_UnknownId_ReturnsFalse()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).CancelAsync(Guid.NewGuid())).ShouldBeFalse();
    }
}
