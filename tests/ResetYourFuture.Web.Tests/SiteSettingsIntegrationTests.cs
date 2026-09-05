using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class SiteSettingsIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public SiteSettingsIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetBackgroundImage_NotConfigured_Returns404()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/site/background-image")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadBackgroundImage_Student_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "bg.png" }
        };

        (await client.PostAsync("/api/admin/site/background-image", content)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
