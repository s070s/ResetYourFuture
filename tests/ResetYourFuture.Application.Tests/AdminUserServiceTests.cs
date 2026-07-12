using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

/// <summary>
/// DeleteUserAsync tests run on SQLite (not InMemory) because the fix under test is
/// about real FK behavior: Restrict FKs from chat/call history and the
/// Certificate→Enrollment NoAction diamond.
/// </summary>
public class AdminUserServiceTests
{
    private static AdminUserService NewService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        var roleManager = Substitute.For<RoleManager<IdentityRole>>(
            Substitute.For<IRoleStore<IdentityRole>>(), null, null, null, null);

        return new AdminUserService(
            userManager,
            roleManager,
            Substitute.For<ITokenService>(),
            NullLogger<AdminUserService>.Instance,
            db,
            Substitute.For<IEmailService>());
    }

    /// <summary>
    /// UserManager substitute that mirrors the real UserStore over the same scoped context:
    /// FindByIdAsync reads from the context, and DeleteAsync removes the user then calls
    /// SaveChangesAsync — flushing whatever the service staged in the change tracker,
    /// exactly like production where both share one ApplicationDbContext.
    /// </summary>
    private static UserManager<ApplicationUser> UserManagerOver(ApplicationDbContext db)
    {
        var userManager = IdentityMocks.MockUserManager();

        userManager.FindByIdAsync(Arg.Any<string>())
            .Returns(ci => db.Users.SingleOrDefaultAsync(u => u.Id == ci.Arg<string>()));

        userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(false);

        userManager.DeleteAsync(Arg.Any<ApplicationUser>())
            .Returns(async ci =>
            {
                db.Users.Remove(ci.Arg<ApplicationUser>());
                await db.SaveChangesAsync();
                return IdentityResult.Success;
            });

        return userManager;
    }

    private static ApplicationUser NewUser(string id) => new()
    {
        Id = id,
        UserName = $"{id}@test.local",
        Email = $"{id}@test.local",
        FirstName = "First",
        LastName = "Last"
    };

    [Fact]
    public async Task DeleteUser_WithChatCallCertificateHistory_DeletesUserAndHistory()
    {
        await using var db = DbContextFactory.CreateSqlite();
        var victim = NewUser("victim");
        var other = NewUser("other");
        db.Users.AddRange(victim, other);

        // Chat history: conversation between the two, messages in both directions.
        var conversation = new ChatConversation { CreatorId = victim.Id, ParticipantId = other.Id };
        db.ChatConversations.Add(conversation);
        db.ChatMessages.Add(new ChatMessage { ConversationId = conversation.Id, SenderId = victim.Id, Content = "hi" });
        db.ChatMessages.Add(new ChatMessage { ConversationId = conversation.Id, SenderId = other.Id, Content = "hey" });

        // Call history: one session initiated by the victim, one by the other user —
        // the victim is a participant in both.
        var victimCall = new CallSession { InitiatorId = victim.Id };
        var otherCall = new CallSession { InitiatorId = other.Id };
        db.CallSessions.AddRange(victimCall, otherCall);
        db.CallParticipants.Add(new CallParticipant { CallSessionId = victimCall.Id, UserId = victim.Id });
        db.CallParticipants.Add(new CallParticipant { CallSessionId = victimCall.Id, UserId = other.Id });
        db.CallParticipants.Add(new CallParticipant { CallSessionId = otherCall.Id, UserId = victim.Id });
        db.CallParticipants.Add(new CallParticipant { CallSessionId = otherCall.Id, UserId = other.Id });

        // Certificate→Enrollment NoAction diamond.
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
        db.Courses.Add(course);
        var enrollment = new Enrollment { Id = Guid.NewGuid(), UserId = victim.Id, CourseId = course.Id };
        db.Enrollments.Add(enrollment);
        db.Certificates.Add(new Certificate
        {
            Id = Guid.NewGuid(),
            UserId = victim.Id,
            EnrollmentId = enrollment.Id,
            CourseId = course.Id,
            RecipientName = "First Last",
            CourseTitleEn = "C"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await NewService(db, UserManagerOver(db)).DeleteUserAsync(victim.Id);

        result.IsSuccess.ShouldBeTrue();
        (await db.Users.AnyAsync(u => u.Id == victim.Id)).ShouldBeFalse();
        (await db.ChatConversations.CountAsync()).ShouldBe(0);
        (await db.ChatMessages.CountAsync()).ShouldBe(0);
        (await db.Certificates.CountAsync()).ShouldBe(0);
        (await db.Enrollments.CountAsync()).ShouldBe(0);
        // The victim's own session is gone with all its participant rows; the other
        // user's session survives with only the victim's participant row removed.
        (await db.CallSessions.SingleAsync()).Id.ShouldBe(otherCall.Id);
        (await db.CallParticipants.SingleAsync()).UserId.ShouldBe(other.Id);
        (await db.Users.AnyAsync(u => u.Id == other.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteUser_WithoutHistory_Succeeds()
    {
        await using var db = DbContextFactory.CreateSqlite();
        db.Users.Add(NewUser("plain"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await NewService(db, UserManagerOver(db)).DeleteUserAsync("plain");

        result.IsSuccess.ShouldBeTrue();
        (await db.Users.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteUser_Missing_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateSqlite();

        var result = await NewService(db, UserManagerOver(db)).DeleteUserAsync("nope");

        result.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task DeleteUser_AdminAccount_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateSqlite();
        db.Users.Add(NewUser("admin"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var userManager = UserManagerOver(db);
        userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), "Admin").Returns(true);

        var result = await NewService(db, userManager).DeleteUserAsync("admin");

        result.StatusCode.ShouldBe(400);
        (await db.Users.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteUser_ConstraintViolation_ReturnsConflict()
    {
        await using var db = DbContextFactory.CreateSqlite();
        db.Users.Add(NewUser("blocked"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var userManager = UserManagerOver(db);
        userManager.DeleteAsync(Arg.Any<ApplicationUser>())
            .ThrowsAsync(new DbUpdateException("FK violation"));

        var result = await NewService(db, userManager).DeleteUserAsync("blocked");

        result.StatusCode.ShouldBe(409);
        result.ErrorMessage.ShouldNotBeNullOrEmpty();
    }
}
