using System.Net;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// Auth-matrix coverage for the remaining AdminOnly CRUD controllers
/// (modules / lessons / assessments). Read endpoints return 200 for admins (empty data)
/// and 403 for students.
/// </summary>
[Collection("web")]
public class AdminCrudAuthMatrixTests
{
    private readonly CustomWebAppFactory _factory;

    public AdminCrudAuthMatrixTests(CustomWebAppFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/admin/assessments")]
    public async Task AdminList_Admin_Returns200(string url)
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        (await client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminModulesByCourse_Admin_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        (await client.GetAsync($"/api/admin/modules/course/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminLessonsByModule_Admin_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        (await client.GetAsync($"/api/admin/lessons/module/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/admin/assessments")]
    [InlineData("/api/admin/modules/course/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/admin/lessons/module/00000000-0000-0000-0000-000000000000")]
    public async Task AdminEndpoints_Student_Returns403(string url)
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/admin/assessments")]
    [InlineData("/api/admin/modules/course/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/admin/lessons/module/00000000-0000-0000-0000-000000000000")]
    public async Task AdminEndpoints_Anonymous_Returns401(string url)
    {
        var client = _factory.CreateClient();

        (await client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
