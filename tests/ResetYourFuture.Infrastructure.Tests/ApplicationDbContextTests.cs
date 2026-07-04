using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Infrastructure.Tests;

public class ApplicationDbContextTests
{
    private static ApplicationDbContext CtxWithUser(string? userId)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        if (userId is not null)
        {
            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
            };
            accessor.HttpContext.Returns(http);
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options, accessor);
    }

    [Fact]
    public async Task SaveChanges_StampsAuditFieldsFromPrincipal()
    {
        await using var db = CtxWithUser("auditor-1");
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };

        db.Courses.Add(course);
        await db.SaveChangesAsync();

        course.CreatedByUserId.ShouldBe("auditor-1");
        course.UpdatedByUserId.ShouldBe("auditor-1");
        course.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveChanges_NoHttpContext_StampsTimestampsButNullUser()
    {
        await using var db = CtxWithUser(null);
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };

        db.Courses.Add(course);
        await db.SaveChangesAsync();

        course.CreatedByUserId.ShouldBeNull();
        course.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveChanges_Modify_DoesNotOverwriteCreatedBy()
    {
        await using var db = CtxWithUser("creator");
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        course.TitleEn = "Updated";
        await db.SaveChangesAsync();

        course.CreatedByUserId.ShouldBe("creator");
    }

    [Fact]
    public async Task SoftDeleteFilter_HidesDeletedEntities()
    {
        await using var db = CtxWithUser("admin");
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        course.IsDeleted = true;
        await db.SaveChangesAsync();

        (await db.Courses.CountAsync()).ShouldBe(0);
        (await db.Courses.IgnoreQueryFilters().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DependentFilter_HidesEnrollmentsOfSoftDeletedCourse()
    {
        await using var db = CtxWithUser("admin");
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
        db.Courses.Add(course);
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = "u1", CourseId = course.Id });
        await db.SaveChangesAsync();

        course.IsDeleted = true;
        await db.SaveChangesAsync();

        (await db.Enrollments.CountAsync()).ShouldBe(0);
        (await db.Enrollments.IgnoreQueryFilters().CountAsync()).ShouldBe(1);
    }
}
