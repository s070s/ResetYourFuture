using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection("web")]
public class CrossCuttingTests
{
    private readonly CustomWebAppFactory _factory;

    public CrossCuttingTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task ProtectedEndpoint_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/courses");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_Student_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        var response = await client.GetAsync("/api/admin/courses");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminEndpoint_Admin_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.GetAsync("/api/admin/courses");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/blog/summaries");

        response.Headers.Contains("X-Content-Type-Options").ShouldBeTrue();
        response.Headers.Contains("X-Frame-Options").ShouldBeTrue();
        response.Headers.Contains("Referrer-Policy").ShouldBeTrue();
        response.Headers.Contains("Permissions-Policy").ShouldBeTrue();
    }

    [Fact]
    public async Task UnknownRoute_RendersNotFoundPage_NotBlank()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/this-route-does-not-exist-e2e");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldContain("Page not found");
        body.ShouldNotContain("Reset Your Future - Home Page");
    }

    [Fact]
    public async Task UnknownApiRoute_StaysJsonProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/this-endpoint-does-not-exist-e2e");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        body.ShouldNotContain("Page not found");
    }
}
