using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.ApiServices;
using ResetYourFuture.Web.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Infrastructure.Tests;

public class TokenServiceTests
{
    private static IConfiguration Config(string? key = "test-signing-key-at-least-32-bytes-long-1234567890") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = key,
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:AccessTokenExpirationMinutes"] = "60"
        }).Build();

    private static ApplicationUser User() => new()
    {
        Id = "user-1",
        Email = "user@example.com",
        UserName = "user@example.com",
        FirstName = "John",
        LastName = "Doe",
        Status = UserStatus.Student,
        IsEnabled = true,
        SecurityStamp = "stamp-1"
    };

    private static TokenService NewService(SubscriptionTierEnum tier = SubscriptionTierEnum.Pro, params string[] roles)
    {
        var um = IdentityMocks.MockUserManager();
        um.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(roles.ToList());
        var subs = Substitute.For<ISubscriptionService>();
        subs.GetUserTierAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(tier);
        return new TokenService(Config(), um, subs);
    }

    [Fact]
    public void Constructor_MissingJwtKey_Throws()
    {
        var um = IdentityMocks.MockUserManager();
        var subs = Substitute.For<ISubscriptionService>();

        Should.Throw<InvalidOperationException>(() => new TokenService(Config(key: null), um, subs));
    }

    [Fact]
    public async Task GenerateAccessToken_EmbedsExpectedClaims()
    {
        var svc = NewService(SubscriptionTierEnum.Pro, "Student");

        var (token, _) = await svc.GenerateAccessTokenAsync(User());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.ShouldContain(c => c.Type == "sub" && c.Value == "user-1");
        jwt.Claims.ShouldContain(c => c.Type == "email" && c.Value == "user@example.com");
        jwt.Claims.ShouldContain(c => c.Type == "firstName" && c.Value == "John");
        jwt.Claims.ShouldContain(c => c.Type == "subscriptionTier" && c.Value == "2");
        jwt.Claims.ShouldContain(c => c.Type == "securityStamp" && c.Value == "stamp-1");
        jwt.Claims.ShouldContain(c => c.Value == "Student");
    }

    [Fact]
    public async Task GenerateAccessToken_ExpiresAroundConfiguredMinutes()
    {
        var svc = NewService();

        var (_, expiration) = await svc.GenerateAccessTokenAsync(User());

        expiration.ShouldBeInRange(DateTime.UtcNow.AddMinutes(58), DateTime.UtcNow.AddMinutes(62));
    }

    [Fact]
    public async Task GenerateImpersonationToken_IncludesImpersonatedByClaim()
    {
        var svc = NewService(SubscriptionTierEnum.Plus, "Student");

        var (token, _) = await svc.GenerateImpersonationTokenAsync(User(), adminId: "admin-9");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.ShouldContain(c => c.Type == "impersonatedBy" && c.Value == "admin-9");
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsDistinct64ByteBase64()
    {
        var svc = NewService();

        var a = svc.GenerateRefreshToken();
        var b = svc.GenerateRefreshToken();

        a.ShouldNotBe(b);
        Convert.FromBase64String(a).Length.ShouldBe(64);
    }
}
