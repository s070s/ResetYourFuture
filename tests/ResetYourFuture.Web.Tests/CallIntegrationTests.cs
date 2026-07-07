using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection("web")]
public class CallIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public CallIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Negotiate_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/call/negotiate?negotiateVersion=1", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
