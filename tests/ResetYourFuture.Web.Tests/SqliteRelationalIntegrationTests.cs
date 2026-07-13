using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// Integration tests that run against the real relational SQLite provider
/// (<see cref="SqliteWebAppFactory"/>) rather than EF Core InMemory, so they exercise the
/// constraint semantics production depends on but the InMemory suite structurally cannot (TEST-1).
/// The flagship case is the enrollment <c>(UserId, CourseId)</c> unique index, whose enforcement
/// is the whole premise of the <c>DbUpdateException</c> catch in
/// <c>CourseService.EnrollAsync</c> — a path that can only ever be hit against a provider that
/// actually has the index.
/// </summary>
[Collection("web-sqlite")]
public class SqliteRelationalIntegrationTests
{
    private readonly SqliteWebAppFactory _factory;

    public SqliteRelationalIntegrationTests(SqliteWebAppFactory factory) => _factory = factory;

    private async Task<(string UserId, Guid CourseId)> SeedUserAndCourseAsync()
    {
        // A real, valid client seeds a real ApplicationUser row (SQLite enforces the Enrollment→User
        // foreign key, so a fabricated UserId would fail for the wrong reason).
        var (_, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var courseId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Courses.Add(new Course { Id = courseId, TitleEn = $"Course-{Guid.NewGuid():N}", IsPublished = true });
        await db.SaveChangesAsync();
        return (userId, courseId);
    }

    private static Enrollment NewEnrollment(string userId, Guid courseId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CourseId = courseId,
        EnrolledAt = DateTime.UtcNow,
        Status = EnrollmentStatus.Active
    };

    [Fact]
    public async Task Enrollment_DuplicateUserCourse_ViolatesUniqueIndex()
    {
        var (userId, courseId) = await SeedUserAndCourseAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Enrollments.Add(NewEnrollment(userId, courseId));
            await db.SaveChangesAsync();
        }

        // A second enrollment for the same (UserId, CourseId) must be rejected by the relational
        // unique index — exactly the failure CourseService.EnrollAsync catches to resolve the race.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Enrollments.Add(NewEnrollment(userId, courseId));
            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Enrollments.CountAsync(e => e.UserId == userId && e.CourseId == courseId))
                .ShouldBe(1);
        }
    }

    [Fact]
    public async Task Enrollment_DuplicateUserCourse_IsSilentlyAllowedByInMemory()
    {
        // The contrast that motivates this whole factory: the InMemory provider enforces no unique
        // index, so the same duplicate the relational schema rejects above is accepted here — which
        // is why the enrollment-race path is untestable on the default integration host. The user
        // and course rows are seeded first because InMemory silently drops rows whose required
        // navigations can't be resolved, which would otherwise mask the point being made.
        using var db = DbContextFactory.CreateInMemory();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "dup@test.com",
            Email = "dup@test.com",
            FirstName = "Dup",
            LastName = "User"
        };
        var courseId = Guid.NewGuid();
        db.Users.Add(user);
        db.Courses.Add(new Course { Id = courseId, TitleEn = "Dup Course", IsPublished = true });
        db.Enrollments.Add(NewEnrollment(user.Id, courseId));
        db.Enrollments.Add(NewEnrollment(user.Id, courseId));

        await Should.NotThrowAsync(() => db.SaveChangesAsync());
        (await db.Enrollments.CountAsync(e => e.UserId == user.Id && e.CourseId == courseId)).ShouldBe(2);
    }

    [Fact]
    public async Task GetCourses_AgainstRelationalProvider_ReturnsSeededCourse()
    {
        // Proves the full request pipeline (auth → controller → CourseService's ordered, filtered
        // query → SQLite) boots and serves data on the relational host, not just that a DbContext works.
        var client = await _factory.CreateAuthenticatedClientAsync("Student");
        var title = $"Relational-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = title, IsPublished = true });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/courses?pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain(title);
    }
}
