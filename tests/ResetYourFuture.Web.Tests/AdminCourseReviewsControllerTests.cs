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

public class AdminCourseReviewsControllerTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AdminCourseReviewsControllerTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<CourseReview> SeedReviewAsync(ReviewStatus status = ReviewStatus.Pending)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = $"Course-{Guid.NewGuid():N}", IsPublished = true };
        db.Courses.Add(course);
        var authorId = $"author-{Guid.NewGuid():N}";
        // The review's author must exist — GetPagedAsync projects r.User.DisplayName inline,
        // and a review whose User navigation can't resolve is silently dropped from results.
        db.Users.Add(new Domain.Identity.ApplicationUser
        {
            Id = authorId, UserName = $"{authorId}@x.com", Email = $"{authorId}@x.com", FirstName = "Author", LastName = "Name"
        });
        var review = new CourseReview { CourseId = course.Id, UserId = authorId, Rating = 4, Body = "text", Status = status };
        db.CourseReviews.Add(review);
        await db.SaveChangesAsync();
        return review;
    }

    [Fact]
    public async Task GetAll_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/admin/course-reviews")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_StudentRole_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/admin/course-reviews")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_Admin_FiltersByStatus()
    {
        await SeedReviewAsync(ReviewStatus.Pending);
        await SeedReviewAsync(ReviewStatus.Approved);
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var pending = await client.GetFromJsonAsync<PagedResult<AdminCourseReviewDto>>("/api/admin/course-reviews?status=Pending");

        pending!.Items.ShouldNotBeEmpty();
        pending.Items.ShouldAllBe(r => r.Status == "Pending");
    }

    [Fact]
    public async Task Approve_Admin_FlipsStatusTo200()
    {
        var review = await SeedReviewAsync(ReviewStatus.Pending);
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.PostAsync($"/api/admin/course-reviews/{review.Id}/approve", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CourseReviews.FindAsync(review.Id))!.Status.ShouldBe(ReviewStatus.Approved);
    }

    [Fact]
    public async Task Reject_Admin_FlipsStatusTo200()
    {
        var review = await SeedReviewAsync(ReviewStatus.Pending);
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        var response = await client.PostAsync($"/api/admin/course-reviews/{review.Id}/reject", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CourseReviews.FindAsync(review.Id))!.Status.ShouldBe(ReviewStatus.Rejected);
    }

    [Fact]
    public async Task Approve_NotFound_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        (await client.PostAsync($"/api/admin/course-reviews/{Guid.NewGuid()}/approve", null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
