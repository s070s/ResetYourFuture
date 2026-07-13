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

/// <summary>
/// The shared "web" test host runs with Assistant:Enabled=false, so search always exercises
/// the SQL title-LIKE fallback — these tests verify the endpoint contract (anonymous access,
/// published-only, query validation), not the semantic-vs-fallback decision (covered by
/// SiteSearchServiceTests).
/// </summary>
[Collection("web")]
public class SearchControllerTests
{
    private readonly CustomWebAppFactory _factory;

    public SearchControllerTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task SeedCourseAsync(string title, bool published)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = title, IsPublished = published });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Search_Anonymous_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/search?q=career");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Search_MatchingPublishedCourse_ReturnsHit()
    {
        var client = _factory.CreateClient();
        var seeded = $"FindMe-{Guid.NewGuid():N}";
        await SeedCourseAsync(seeded, published: true);

        var result = await client.GetFromJsonAsync<SiteSearchResultDto>($"/api/search?q={seeded}");

        result.ShouldNotBeNull();
        result!.Hits.ShouldContain(h => h.Title == seeded && h.SourceType == "Course");
        result.SemanticSearchUsed.ShouldBeFalse(); // assistant disabled in the test host
    }

    [Fact]
    public async Task Search_UnpublishedCourse_IsExcluded()
    {
        var client = _factory.CreateClient();
        var title = $"Draft-{Guid.NewGuid():N}";
        await SeedCourseAsync(title, published: false);

        var result = await client.GetFromJsonAsync<SiteSearchResultDto>($"/api/search?q={title}");

        result!.Hits.ShouldNotContain(h => h.Title == title);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmptyHitsNot400()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/search?q=");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SiteSearchResultDto>();
        result!.Hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_LimitAboveMax_IsClamped()
    {
        var client = _factory.CreateClient();

        // 25 distinct published courses matching "Clamp" so an unclamped limit=100 would exceed MaxLimit(20).
        for (var i = 0; i < 25; i++)
            await SeedCourseAsync($"Clamp Test {i}-{Guid.NewGuid():N}", published: true);

        var result = await client.GetFromJsonAsync<SiteSearchResultDto>("/api/search?q=Clamp Test&limit=100");

        result!.Hits.Count.ShouldBeLessThanOrEqualTo(20);
    }
}
