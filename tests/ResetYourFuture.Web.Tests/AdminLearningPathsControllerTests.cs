using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class AdminLearningPathsControllerTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AdminLearningPathsControllerTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<LearningPath> SeedPathAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var path = new LearningPath { Id = Guid.NewGuid(), TitleEn = $"Path-{Guid.NewGuid():N}" };
        db.LearningPaths.Add(path);
        await db.SaveChangesAsync();
        return path;
    }

    private async Task<Course> SeedCourseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = $"Course-{Guid.NewGuid():N}", IsPublished = true };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return course;
    }

    [Fact]
    public async Task GetAll_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/admin/paths")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_StudentRole_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/admin/paths")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Admin_ReturnsCreatedUnpublishedPath()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.PostAsJsonAsync("/api/admin/paths",
            new SaveLearningPathRequest("New Path", null, null, null, null, 1));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AdminLearningPathDetailDto>();
        created!.IsPublished.ShouldBeFalse();
        created.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Publish_Admin_FlipsPublishedTo200()
    {
        var path = await SeedPathAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.PostAsync($"/api/admin/paths/{path.Id}/publish", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.LearningPaths.FindAsync(path.Id))!.IsPublished.ShouldBeTrue();
    }

    [Fact]
    public async Task AddStep_Admin_AppendsStep()
    {
        var path = await SeedPathAsync();
        var course = await SeedCourseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.PostAsJsonAsync($"/api/admin/paths/{path.Id}/steps", new AddLearningPathStepRequest(course.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<AdminLearningPathDetailDto>();
        updated!.Steps.ShouldHaveSingleItem().CourseId.ShouldBe(course.Id);
    }

    [Fact]
    public async Task AddStep_DuplicateCourse_ReturnsConflict()
    {
        var path = await SeedPathAsync();
        var course = await SeedCourseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");
        await client.PostAsJsonAsync($"/api/admin/paths/{path.Id}/steps", new AddLearningPathStepRequest(course.Id));

        var response = await client.PostAsJsonAsync($"/api/admin/paths/{path.Id}/steps", new AddLearningPathStepRequest(course.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveStep_Admin_Returns204()
    {
        var path = await SeedPathAsync();
        var course = await SeedCourseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");
        var addResponse = await client.PostAsJsonAsync($"/api/admin/paths/{path.Id}/steps", new AddLearningPathStepRequest(course.Id));
        var added = await addResponse.Content.ReadFromJsonAsync<AdminLearningPathDetailDto>();
        var stepId = added!.Steps.Single().Id;

        var response = await client.DeleteAsync($"/api/admin/paths/{path.Id}/steps/{stepId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_Admin_SoftDeletesPath()
    {
        var path = await SeedPathAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.DeleteAsync($"/api/admin/paths/{path.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/admin/paths/{path.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
