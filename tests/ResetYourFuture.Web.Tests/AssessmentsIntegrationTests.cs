using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class AssessmentsIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AssessmentsIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAssessments_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/assessments")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAssessments_FreeUserWithoutAccess_Returns403()
    {
        // A freshly-seeded Student has no subscription → Free defaults (AssessmentAccess = false).
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/assessments")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMine_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/assessments/mine")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
