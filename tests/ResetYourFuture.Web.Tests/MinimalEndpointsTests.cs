using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class MinimalEndpointsTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public MinimalEndpointsTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthComplete_SetCookie_MaxAgeMatchesRememberMe(bool rememberMe)
    {
        var email = $"remember-me-{Guid.NewGuid():N}@test.com";
        var password = "Test-Pass-1!";
        await _factory.CreateConfirmedUserAsync(email, password);

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var loginResult = await authService.LoginAsync(new LoginRequestDto
        {
            Email = email,
            Password = password,
            RememberMe = rememberMe
        });
        loginResult.Success.ShouldBeTrue();

        var client = NoRedirectClient();
        var response = await client.GetAsync($"/auth/complete?ticket={Uri.EscapeDataString(loginResult.Token!)}&returnUrl=%2F");

        response.Headers.TryGetValues("Set-Cookie", out var setCookieValues).ShouldBeTrue();
        var authCookie = setCookieValues!.FirstOrDefault(v => v.StartsWith(".RYF.Auth="));
        authCookie.ShouldNotBeNull();

        // rememberMe=false must yield a session cookie (no expires/max-age) so it's discarded
        // when the browser closes; rememberMe=true must yield a persistent cookie.
        var hasExpiry = authCookie.Contains("expires=", StringComparison.OrdinalIgnoreCase)
            || authCookie.Contains("max-age=", StringComparison.OrdinalIgnoreCase);
        hasExpiry.ShouldBe(rememberMe, $"Set-Cookie was: {authCookie}");
    }

    [Fact]
    public async Task Sitemap_Returns200Xml()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/sitemap.xml");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
    }

    [Fact]
    public async Task AuthComplete_InvalidTicket_RedirectsToLogin()
    {
        var client = NoRedirectClient();

        var response = await client.GetAsync("/auth/complete?ticket=garbage&returnUrl=%2F");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/login");
    }

    [Fact]
    public async Task Signout_RedirectsLocally()
    {
        var client = NoRedirectClient();

        var response = await client.GetAsync("/auth/signout?returnUrl=%2F");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task CultureSet_RedirectsLocally()
    {
        var client = NoRedirectClient();

        var response = await client.GetAsync("/culture/set?culture=el-GR&returnUrl=%2F");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task HealthLive_AnonymousReturns200_WithNoDependencyChecks()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_AnonymousReturns200_AgainstTheTestDatabase()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
