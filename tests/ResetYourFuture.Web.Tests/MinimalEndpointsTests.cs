using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection("web")]
public class MinimalEndpointsTests
{
    private readonly CustomWebAppFactory _factory;

    public MinimalEndpointsTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

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
}
