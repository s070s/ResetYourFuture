using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Infrastructure.Tests;

public class AuthServiceTests
{
    private sealed record Harness(
        AuthService Svc,
        UserManager<ApplicationUser> Um,
        SignInManager<ApplicationUser> Sm,
        ISubscriptionService Subs,
        EphemeralDataProtectionProvider Dp,
        IHttpContextAccessor Accessor);

    private static Harness Build()
    {
        var um = IdentityMocks.MockUserManager();
        var sm = IdentityMocks.MockSignInManager(um);
        var subs = Substitute.For<ISubscriptionService>();
        var accessor = Substitute.For<IHttpContextAccessor>();
        var dp = new EphemeralDataProtectionProvider();
        var ctx = DbContextFactory.CreateInMemory();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-32-bytes-long-1234567890",
            ["Jwt:Issuer"] = "iss",
            ["Jwt:Audience"] = "aud",
            ["Jwt:AccessTokenExpirationMinutes"] = "60"
        }).Build();

        var svc = new AuthService(accessor, um, sm, subs, ctx, dp, config, NullLogger<AuthService>.Instance);
        return new Harness(svc, um, sm, subs, dp, accessor);
    }

    private static ApplicationUser User(bool enabled = true) =>
        new() { Id = "u1", Email = "u@x.com", UserName = "u@x.com", FirstName = "F", LastName = "L", IsEnabled = enabled };

    private static LoginRequestDto Login() => new() { Email = "u@x.com", Password = "Password1" };

    // ---- LoginAsync ----------------------------------------------------------

    [Fact]
    public async Task Login_UnknownEmail_Fails()
    {
        var h = Build();
        h.Um.FindByEmailAsync("u@x.com").Returns((ApplicationUser?)null);

        var result = await h.Svc.LoginAsync(Login());

        result.Success.ShouldBeFalse();
        result.Message.ShouldBe("Invalid credentials.");
    }

    [Fact]
    public async Task Login_EmailNotConfirmed_Fails()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync("u@x.com").Returns(user);
        h.Um.IsEmailConfirmedAsync(user).Returns(false);

        (await h.Svc.LoginAsync(Login())).Message.ShouldBe("Email not confirmed.");
    }

    [Fact]
    public async Task Login_DisabledAccount_Fails()
    {
        var h = Build();
        var user = User(enabled: false);
        h.Um.FindByEmailAsync("u@x.com").Returns(user);
        h.Um.IsEmailConfirmedAsync(user).Returns(true);

        (await h.Svc.LoginAsync(Login())).Message!.ShouldContain("disabled");
    }

    [Fact]
    public async Task Login_BadPassword_Fails()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync("u@x.com").Returns(user);
        h.Um.IsEmailConfirmedAsync(user).Returns(true);
        h.Sm.CheckPasswordSignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(SignInResult.Failed);

        (await h.Svc.LoginAsync(Login())).Message.ShouldBe("Invalid credentials.");
    }

    [Fact]
    public async Task Login_LockedOut_Fails()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync("u@x.com").Returns(user);
        h.Um.IsEmailConfirmedAsync(user).Returns(true);
        h.Sm.CheckPasswordSignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(SignInResult.LockedOut);

        (await h.Svc.LoginAsync(Login())).Message.ShouldBe("Account locked. Try again later.");
    }

    [Fact]
    public async Task Login_Success_ReturnsProtectedTicketEncodingUserId()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync("u@x.com").Returns(user);
        h.Um.IsEmailConfirmedAsync(user).Returns(true);
        h.Um.GetSecurityStampAsync(user).Returns("stamp-1");
        h.Sm.CheckPasswordSignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(SignInResult.Success);

        var result = await h.Svc.LoginAsync(Login());

        result.Success.ShouldBeTrue();
        result.Token.ShouldNotBeNull();
        var payload = h.Dp.CreateProtector(AuthService.ProtectorPurpose).ToTimeLimitedDataProtector().Unprotect(result.Token!);
        payload.ShouldStartWith("u1|");
    }

    // ---- RegisterAsync -------------------------------------------------------

    [Fact]
    public async Task Register_Success_AssignsRolePlanAndReturnsMessage()
    {
        var h = Build();
        h.Um.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        h.Um.AddToRoleAsync(Arg.Any<ApplicationUser>(), "Student").Returns(IdentityResult.Success);
        h.Um.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>()).Returns("token");

        var result = await h.Svc.RegisterAsync(new RegisterRequestDto
        {
            Email = "u@x.com",
            Password = "Password1",
            ConfirmPassword = "Password1",
            FirstName = "F",
            LastName = "L",
            GdprConsent = true
        });

        result.Success.ShouldBeTrue();
        result.Message!.ShouldContain("Registration successful");
        await h.Subs.Received().AssignFreePlanAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Register_CreateFails_ReturnsErrors()
    {
        var h = Build();
        h.Um.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var result = await h.Svc.RegisterAsync(new RegisterRequestDto
        {
            Email = "u@x.com",
            Password = "weak",
            ConfirmPassword = "weak",
            FirstName = "F",
            LastName = "L",
            GdprConsent = true
        });

        result.Success.ShouldBeFalse();
        result.Errors!.ShouldContain("Password too weak");
    }

    // ---- Forgot / Reset ------------------------------------------------------

    [Fact]
    public async Task ForgotPassword_UnknownUser_ReturnsGenericSuccess()
    {
        var h = Build();
        h.Um.FindByEmailAsync("u@x.com").Returns((ApplicationUser?)null);

        var result = await h.Svc.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "u@x.com" });

        result.Success.ShouldBeTrue();
        result.Message!.ShouldContain("If the email exists");
    }

    [Fact]
    public async Task ResetPassword_UnknownUser_Fails()
    {
        var h = Build();
        h.Um.FindByEmailAsync("u@x.com").Returns((ApplicationUser?)null);

        var result = await h.Svc.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = "u@x.com",
            Token = "t",
            NewPassword = "Password1",
            ConfirmPassword = "Password1"
        });

        result.Success.ShouldBeFalse();
        result.Message.ShouldBe("Invalid request.");
    }

    [Fact]
    public async Task ResetPassword_Success()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync("u@x.com").Returns(user);
        h.Um.ResetPasswordAsync(user, "t", "Password1").Returns(IdentityResult.Success);

        var result = await h.Svc.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = "u@x.com",
            Token = "t",
            NewPassword = "Password1",
            ConfirmPassword = "Password1"
        });

        result.Success.ShouldBeTrue();
    }

    // ---- Misc ----------------------------------------------------------------

    [Fact]
    public async Task Logout_ReturnsSignoutUrl()
    {
        (await Build().Svc.LogoutAsync()).ShouldBe("/auth/signout?returnUrl=%2F");
    }

    [Fact]
    public async Task Impersonate_TargetMissing_Fails()
    {
        var h = Build();
        h.Um.FindByIdAsync("target").Returns((ApplicationUser?)null);

        var result = await h.Svc.ImpersonateAsync("target");

        result.Success.ShouldBeFalse();
        result.Message.ShouldBe("User not found.");
    }

    [Fact]
    public async Task IsAuthenticated_ReflectsPrincipal()
    {
        var h = Build();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "cookie"))
        };
        h.Accessor.HttpContext.Returns(http);

        (await h.Svc.IsAuthenticatedAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task IsImpersonating_NoBackupCookie_ReturnsFalse()
    {
        var h = Build();
        h.Accessor.HttpContext.Returns(new DefaultHttpContext());

        (await h.Svc.IsImpersonatingAsync()).ShouldBeFalse();
    }
}
