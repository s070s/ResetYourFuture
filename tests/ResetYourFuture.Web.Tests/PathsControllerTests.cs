using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// Public learning-path catalog: anonymous access (like <see cref="SearchControllerTests"/>),
/// published-only visibility, and per-user progress projection.
/// </summary>
public class PathsControllerTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public PathsControllerTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<(Course course, LearningPath path)> SeedPublishedPathAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = $"Course-{Guid.NewGuid():N}", IsPublished = true };
        db.Courses.Add(course);
        var path = new LearningPath { Id = Guid.NewGuid(), TitleEn = $"Path-{Guid.NewGuid():N}", IsPublished = true };
        db.LearningPaths.Add(path);
        db.LearningPathSteps.Add(new LearningPathStep { LearningPathId = path.Id, CourseId = course.Id, StepOrder = 1 });
        await db.SaveChangesAsync();
        return (course, path);
    }

    [Fact]
    public async Task GetPaths_Anonymous_Returns200()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/paths")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPaths_ExcludesUnpublished()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = $"Draft-{Guid.NewGuid():N}";
        db.LearningPaths.Add(new LearningPath { Id = Guid.NewGuid(), TitleEn = title, IsPublished = false });
        await db.SaveChangesAsync();
        var client = _factory.CreateClient();

        var result = await client.GetFromJsonAsync<List<LearningPathListItemDto>>("/api/paths");

        result!.ShouldNotContain(p => p.Title == title);
    }

    [Fact]
    public async Task GetPath_UnknownId_Returns404()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync($"/api/paths/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPath_Anonymous_StepsAreNotLocked()
    {
        var (_, path) = await SeedPublishedPathAsync();
        var client = _factory.CreateClient();

        var result = await client.GetFromJsonAsync<LearningPathDetailDto>($"/api/paths/{path.Id}");

        result.ShouldNotBeNull();
        result!.Steps.ShouldHaveSingleItem().IsLocked.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPath_AuthenticatedUser_ReflectsCompletion()
    {
        var (course, path) = await SeedPublishedPathAsync();
        var (client, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = userId, CourseId = course.Id, Status = EnrollmentStatus.Completed });
            await db.SaveChangesAsync();
        }

        var result = await client.GetFromJsonAsync<LearningPathDetailDto>($"/api/paths/{path.Id}");

        result!.Steps.ShouldHaveSingleItem().IsCompleted.ShouldBeTrue();
    }
}
