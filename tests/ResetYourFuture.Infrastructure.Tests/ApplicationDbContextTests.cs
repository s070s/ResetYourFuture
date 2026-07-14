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

    [Fact]
    public async Task CategoryNameEn_Duplicate_ThrowsOnSave()
    {
        // DB-5: the filtered unique index is a DB-level backstop to the racy
        // check-then-insert in AdminCategoryService — verify it actually exists and fires,
        // against SQLite (the same provider the earlier "breaks SQLite" rationale doubted).
        await using var db = ResetYourFuture.TestSupport.DbContextFactory.CreateSqlite();
        db.Categories.Add(new Category { Id = Guid.NewGuid(), NameEn = "Career" });
        await db.SaveChangesAsync();

        db.Categories.Add(new Category { Id = Guid.NewGuid(), NameEn = "Career" });
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CategoryNameEn_DuplicateButOneSoftDeleted_Allowed()
    {
        // The filter is `[IsDeleted] = 0` — a soft-deleted category must not block reusing
        // its name for a new, active one.
        await using var db = ResetYourFuture.TestSupport.DbContextFactory.CreateSqlite();
        db.Categories.Add(new Category { Id = Guid.NewGuid(), NameEn = "Career", IsDeleted = true });
        await db.SaveChangesAsync();

        db.Categories.Add(new Category { Id = Guid.NewGuid(), NameEn = "Career" });
        await db.SaveChangesAsync();

        (await db.Categories.IgnoreQueryFilters().CountAsync(c => c.NameEn == "Career")).ShouldBe(2);
    }

    [Fact]
    public async Task DateTimeProperty_MaterializesWithUtcKind()
    {
        // DB-6: was Kind=Unspecified on read (only *.UtcNow-by-convention, nothing pinned the
        // Kind on materialization) — a fresh context instance re-reading a persisted row is the
        // only way to prove the converter runs on read, not just echoes back the in-memory value.
        var dbName = Guid.NewGuid().ToString("N");
        Guid courseId;
        await using (var db = CtxWithUser("u", dbName))
        {
            var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
            courseId = course.Id;
            db.Courses.Add(course);
            db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = "u1", CourseId = course.Id });
            await db.SaveChangesAsync();
        }

        await using var freshDb = CtxWithUser("u", dbName);
        var enrollment = await freshDb.Enrollments.AsNoTracking().FirstAsync(e => e.CourseId == courseId);

        enrollment.EnrolledAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ConcurrentEdit_StaleRowVersion_ThrowsConcurrencyException()
    {
        // DB-7: RowVersion is DB-generated in production (SQL Server's rowversion type is
        // auto-incremented by the engine itself on every UPDATE — not something the app sets,
        // and not something the InMemory test provider can simulate on its own). This proves
        // EF's optimistic-concurrency check is correctly wired for Lesson — the same WHERE-clause
        // comparison AdminLessonsController.UpdateLesson's try/catch relies on — by manufacturing
        // the "stale original value" state a real concurrent SQL Server write would leave behind.
        var dbName = Guid.NewGuid().ToString("N");
        Guid lessonId;
        await using (var seedDb = CtxWithUser("seed", dbName))
        {
            var seedCourse = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
            var seedModule = new Module { Id = Guid.NewGuid(), TitleEn = "M", CourseId = seedCourse.Id };
            var seedLesson = new Lesson { Id = Guid.NewGuid(), TitleEn = "L", ModuleId = seedModule.Id };
            lessonId = seedLesson.Id;
            seedDb.Courses.Add(seedCourse);
            seedDb.Modules.Add(seedModule);
            seedDb.Lessons.Add(seedLesson);
            await seedDb.SaveChangesAsync();
        }

        await using var db = CtxWithUser("editor", dbName);
        var lesson = await db.Lessons.FirstAsync(l => l.Id == lessonId);
        lesson.TitleEn = "Edited";

        // Simulate another writer having already changed the row version in between this
        // request's read and its save (what SQL Server would really do on a concurrent UPDATE).
        db.Entry(lesson).OriginalValues[nameof(Lesson.RowVersion)] = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }
}
