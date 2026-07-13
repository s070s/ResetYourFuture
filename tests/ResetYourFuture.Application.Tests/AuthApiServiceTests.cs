using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class AuthApiServiceTests
{
    private sealed record Harness(AuthApiService Svc, UserManager<ApplicationUser> Um, ApplicationDbContext Db, ITokenService Tok);

    private static Harness Build()
    {
        var um = IdentityMocks.MockUserManager();
        var sm = IdentityMocks.MockSignInManager(um);
        var tok = Substitute.For<ITokenService>();
        tok.GenerateAccessTokenAsync(Arg.Any<ApplicationUser>())
            .Returns(Task.FromResult(("access-token", DateTime.UtcNow.AddMinutes(15))));
        tok.GenerateRefreshToken().Returns(_ => "new-plain-refresh-" + Guid.NewGuid().ToString("N"));
        var subs = Substitute.For<ISubscriptionService>();
        var email = Substitute.For<IEmailService>();
        var db = DbContextFactory.CreateInMemory();

        var svc = new AuthApiService(um, sm, tok, subs, NullLogger<AuthApiService>.Instance, db, email);
        return new Harness(svc, um, db, tok);
    }

    private static ApplicationUser User(string id = "u1", bool enabled = true, string stamp = "stamp-1") =>
        new() { Id = id, Email = "u@x.com", UserName = "u@x.com", FirstName = "F", LastName = "L", IsEnabled = enabled, SecurityStamp = stamp };

    // HashToken is private static — reflect into it so tests can seed a RefreshToken row whose
    // TokenHash matches what the service will compute for a given plaintext token.
    private static string HashToken(string plain)
    {
        var method = typeof(AuthApiService).GetMethod("HashToken", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string)method.Invoke(null, [plain])!;
    }

    // Include(rt => rt.User) needs a real matching row in the DB — mocking UserManager alone
    // isn't enough (same EF InMemory "silently drops rows on unresolvable required navigation"
    // gotcha as elsewhere in this suite). Idempotent so callers can seed the same user once.
    private static void EnsureUserInDb(ApplicationDbContext db, ApplicationUser user)
    {
        if (!db.Users.Local.Any(u => u.Id == user.Id))
            db.Users.Add(user);
    }

    private static RefreshToken Seed(ApplicationDbContext db, ApplicationUser user, string plainToken,
        string? securityStampAtIssuance, DateTimeOffset? expiresAt = null, DateTimeOffset? revokedAt = null, Guid? replacedByTokenId = null)
    {
        EnsureUserInDb(db, user);
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(plainToken),
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            SecurityStampAtIssuance = securityStampAtIssuance,
            RevokedAt = revokedAt,
            ReplacedByTokenId = replacedByTokenId
        };
        db.RefreshTokens.Add(token);
        return token;
    }

    // ---- RefreshAsync: baseline behaviour -------------------------------------

    [Fact]
    public async Task Refresh_ValidToken_RotatesAndStampsNewToken()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync(user.Email!).Returns(user);
        Seed(h.Db, user, "plain-1", securityStampAtIssuance: user.SecurityStamp);
        await h.Db.SaveChangesAsync();

        var result = await h.Svc.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "plain-1" });

        result.Value!.Success.ShouldBeTrue();
        result.Value.RefreshToken.ShouldNotBeNullOrEmpty();
        var rows = await h.Db.RefreshTokens.ToListAsync();
        rows.Count.ShouldBe(2);
        var oldRow = rows.Single(r => r.TokenHash == HashToken("plain-1"));
        oldRow.RevokedAt.ShouldNotBeNull();
        oldRow.ReplacedByTokenId.ShouldNotBeNull();
        var newRow = rows.Single(r => r.Id == oldRow.ReplacedByTokenId);
        newRow.SecurityStampAtIssuance.ShouldBe(user.SecurityStamp);
        newRow.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Refresh_UnknownToken_Rejects()
    {
        var h = Build();

        var result = await h.Svc.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "does-not-exist" });

        result.Value!.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Rejects()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync(user.Email!).Returns(user);
        Seed(h.Db, user, "plain-1", user.SecurityStamp, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        await h.Db.SaveChangesAsync();

        var result = await h.Svc.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "plain-1" });

        result.Value!.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_DisabledUser_RevokesTokenAndRejects()
    {
        var h = Build();
        var user = User(enabled: false);
        // SecurityStampAtIssuance must match, or the stamp check (which runs before the
        // enabled check) would also legitimately reject it — isolate the assertion.
        Seed(h.Db, user, "plain-1", user.SecurityStamp);
        await h.Db.SaveChangesAsync();

        var result = await h.Svc.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "plain-1" });

        result.Value!.Success.ShouldBeFalse();
        (await h.Db.RefreshTokens.SingleAsync()).RevokedAt.ShouldNotBeNull();
    }

    // ---- RefreshAsync: SEC-1 security-stamp check ------------------------------

    [Fact]
    public async Task Refresh_StaleSecurityStamp_RejectsAndRevokesToken()
    {
        // The user's password was reset (rotating SecurityStamp) after this token was issued.
        var h = Build();
        var user = User(stamp: "current-stamp");
        h.Um.FindByEmailAsync(user.Email!).Returns(user);
        Seed(h.Db, user, "plain-1", securityStampAtIssuance: "old-stamp-before-reset");
        await h.Db.SaveChangesAsync();

        var result = await h.Svc.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "plain-1" });

        result.Value!.Success.ShouldBeFalse();
        (await h.Db.RefreshTokens.SingleAsync()).RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Refresh_NullSecurityStampAtIssuance_TreatedAsMismatch()
    {
        // Pre-SEC-1 rows have no stamp recorded — must not be treated as an automatic match.
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync(user.Email!).Returns(user);
        Seed(h.Db, user, "plain-1", securityStampAtIssuance: null);
        await h.Db.SaveChangesAsync();

        var result = await h.Svc.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "plain-1" });

        result.Value!.Success.ShouldBeFalse();
    }

    // ---- RefreshAsync: SEC-1 reuse detection -----------------------------------

    [Fact]
    public async Task Refresh_ReusedRevokedToken_RevokesDescendantChain()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync(user.Email!).Returns(user);

        // Simulate: token A was legitimately rotated to B, and B to C (both still active).
        // An attacker now replays the already-spent token A.
        var c = Seed(h.Db, user, "plain-c", user.SecurityStamp);
        var b = Seed(h.Db, user, "plain-b", user.SecurityStamp, revokedAt: DateTimeOffset.UtcNow, replacedByTokenId: c.Id);
        Seed(h.Db, user, "plain-a", user.SecurityStamp, revokedAt: DateTimeOffset.UtcNow, replacedByTokenId: b.Id);
        await h.Db.SaveChangesAsync();

        var result = await h.Svc.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "plain-a" });

        result.Value!.Success.ShouldBeFalse();
        // B was already revoked (legitimate rotation); C must now be revoked too (chain severed).
        (await h.Db.RefreshTokens.SingleAsync(rt => rt.Id == c.Id)).RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Refresh_ReusedRevokedTokenWithNoDescendant_RejectsWithoutThrowing()
    {
        // A token that was revoked directly (e.g. by the disabled-user or stale-stamp guards)
        // rather than via rotation has no ReplacedByTokenId — the chain walk must handle that.
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync(user.Email!).Returns(user);
        Seed(h.Db, user, "plain-a", user.SecurityStamp, revokedAt: DateTimeOffset.UtcNow, replacedByTokenId: null);
        await h.Db.SaveChangesAsync();

        var result = await h.Svc.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "plain-a" });

        result.Value!.Success.ShouldBeFalse();
    }

    // ---- ResetPasswordAsync: SEC-1 bulk revoke ---------------------------------

    [Fact]
    public async Task ResetPassword_Success_RevokesAllActiveRefreshTokensForThatUser()
    {
        // Also proves ExecuteUpdateAsync behaves correctly against the EF InMemory provider
        // with real matching rows (not just an empty result set).
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync(user.Email!).Returns(user);
        h.Um.ResetPasswordAsync(user, "reset-token", "NewPassword1!").Returns(IdentityResult.Success);
        Seed(h.Db, user, "plain-1", user.SecurityStamp);
        Seed(h.Db, user, "plain-2", user.SecurityStamp);
        var otherUser = User(id: "u2");
        Seed(h.Db, otherUser, "plain-other", otherUser.SecurityStamp);
        await h.Db.SaveChangesAsync();

        var result = await h.Svc.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = user.Email!,
            Token = "reset-token",
            NewPassword = "NewPassword1!",
            ConfirmPassword = "NewPassword1!"
        });

        result.Value!.Success.ShouldBeTrue();
        (await h.Db.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync())
            .ShouldAllBe(rt => rt.RevokedAt != null);
        (await h.Db.RefreshTokens.SingleAsync(rt => rt.UserId == otherUser.Id)).RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ResetPassword_Failure_DoesNotRevokeTokens()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync(user.Email!).Returns(user);
        h.Um.ResetPasswordAsync(user, "bad-token", "x")
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));
        Seed(h.Db, user, "plain-1", user.SecurityStamp);
        await h.Db.SaveChangesAsync();

        await h.Svc.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = user.Email!,
            Token = "bad-token",
            NewPassword = "x",
            ConfirmPassword = "x"
        });

        (await h.Db.RefreshTokens.SingleAsync()).RevokedAt.ShouldBeNull();
    }
}
