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
    private static ApplicationDbContext CtxWithUser(string? userId, string? dbName = null)
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
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString("N"))
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
        // DB-3: UpdatedAt/UpdatedByUserId are no longer stamped on Added — a freshly created
        // row hasn't been "updated" yet, so both stay null until a real Modified save.
        course.UpdatedByUserId.ShouldBeNull();
        course.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChanges_NoHttpContext_StampsTimestampsButNullUser()
    {
        await using var db = CtxWithUser(null);
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };

        db.Courses.Add(course);
        await db.SaveChangesAsync();

        course.CreatedByUserId.ShouldBeNull();
        course.UpdatedAt.ShouldBeNull();
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
    public async Task SaveChanges_Modify_StampsUpdatedAtAndUpdatedByUserId()
    {
        await using var db = CtxWithUser("creator");
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        course.TitleEn = "Updated";
        await db.SaveChangesAsync();

        course.UpdatedByUserId.ShouldBe("creator");
        course.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveChanges_ModifyByDifferentUser_UpdatesUpdatedByUserId()
    {
        // DB-3: this is the bug — `??=` meant UpdatedByUserId was stuck on the first writer
        // forever, once it had been non-null since insertion. A later edit by someone else
        // (a separate DbContext instance, matching a separate HTTP request) must now correctly
        // overwrite it, not keep showing the creator.
        var dbName = Guid.NewGuid().ToString("N");
        Guid courseId;
        await using (var db = CtxWithUser("creator", dbName))
        {
            var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
            courseId = course.Id;
            db.Courses.Add(course);
            await db.SaveChangesAsync();
        }

        await using var editorDb = CtxWithUser("editor-2", dbName);
        var tracked = await editorDb.Courses.FirstAsync(c => c.Id == courseId);
        tracked.TitleEn = "Updated again";
        await editorDb.SaveChangesAsync();

        tracked.UpdatedByUserId.ShouldBe("editor-2");
        tracked.CreatedByUserId.ShouldBe("creator");
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
