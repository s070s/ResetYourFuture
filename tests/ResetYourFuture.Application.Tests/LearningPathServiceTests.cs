using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class LearningPathServiceTests
{
    private const string UserId = "student-1";

    private static LearningPathService NewService(ApplicationDbContext db) =>
        new(db, NullLogger<LearningPathService>.Instance);

    private static Course NewCourse(string title) => new() { Id = Guid.NewGuid(), TitleEn = title, IsPublished = true };

    private static async Task<(ApplicationDbContext db, LearningPath path, Course a, Course b, Course c)> SeedPublishedPathAsync()
    {
        var db = DbContextFactory.CreateInMemory();
        var courseA = NewCourse("Course A");
        var courseB = NewCourse("Course B");
        var courseC = NewCourse("Course C");
        db.Courses.AddRange(courseA, courseB, courseC);

        var path = new LearningPath { Id = Guid.NewGuid(), TitleEn = "Career Starter", IsPublished = true };
        db.LearningPaths.Add(path);
        db.LearningPathSteps.AddRange(
            new LearningPathStep { LearningPathId = path.Id, CourseId = courseA.Id, StepOrder = 1 },
            new LearningPathStep { LearningPathId = path.Id, CourseId = courseB.Id, StepOrder = 2 },
            new LearningPathStep { LearningPathId = path.Id, CourseId = courseC.Id, StepOrder = 3 });

        await db.SaveChangesAsync();
        return (db, path, courseA, courseB, courseC);
    }

    // --- Public catalog ---

    [Fact]
    public async Task GetPublishedAsync_ExcludesUnpublishedPaths()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.LearningPaths.Add(new LearningPath { Id = Guid.NewGuid(), TitleEn = "Draft", IsPublished = false });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetPublishedAsync(userId: null, lang: "en");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPublishedAsync_Anonymous_CompletedStepsIsZero()
    {
        var (db, _, _, _, _) = await SeedPublishedPathAsync();
        await using var _ = db;

        var result = await NewService(db).GetPublishedAsync(userId: null, lang: "en");

        var item = result.ShouldHaveSingleItem();
        item.StepCount.ShouldBe(3);
        item.CompletedSteps.ShouldBe(0);
    }

    [Fact]
    public async Task GetPublishedAsync_AuthenticatedUser_CountsCompletedSteps()
    {
        var (db, _, courseA, courseB, _) = await SeedPublishedPathAsync();
        await using var _ = db;
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = courseA.Id, Status = EnrollmentStatus.Completed });
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = courseB.Id, Status = EnrollmentStatus.Active });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetPublishedAsync(UserId, lang: "en");

        result.ShouldHaveSingleItem().CompletedSteps.ShouldBe(1);
    }

    // --- Public detail / progress projection ---

    [Fact]
    public async Task GetByIdAsync_Unpublished_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var path = new LearningPath { Id = Guid.NewGuid(), TitleEn = "Draft", IsPublished = false };
        db.LearningPaths.Add(path);
        await db.SaveChangesAsync();

        (await NewService(db).GetByIdAsync(path.Id, userId: null, lang: "en")).ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Anonymous_NoStepsAreLockedOrNext()
    {
        var (db, path, _, _, _) = await SeedPublishedPathAsync();
        await using var _ = db;

        var result = await NewService(db).GetByIdAsync(path.Id, userId: null, lang: "en");

        result.ShouldNotBeNull();
        result!.Steps.ShouldAllBe(s => !s.IsLocked && !s.IsNext && !s.IsCompleted);
    }

    [Fact]
    public async Task GetByIdAsync_AuthenticatedUser_ProjectsLockedNextCompleted()
    {
        var (db, path, courseA, courseB, courseC) = await SeedPublishedPathAsync();
        await using var _ = db;
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = courseA.Id, Status = EnrollmentStatus.Completed });
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = courseB.Id, Status = EnrollmentStatus.Active });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetByIdAsync(path.Id, UserId, lang: "en");

        result.ShouldNotBeNull();
        var steps = result!.Steps.OrderBy(s => s.StepOrder).ToList();

        steps[0].CourseId.ShouldBe(courseA.Id);
        steps[0].IsCompleted.ShouldBeTrue();
        steps[0].IsLocked.ShouldBeFalse();
        steps[0].IsNext.ShouldBeFalse();

        steps[1].CourseId.ShouldBe(courseB.Id);
        steps[1].IsCompleted.ShouldBeFalse();
        steps[1].IsLocked.ShouldBeFalse();
        steps[1].IsNext.ShouldBeTrue();

        steps[2].CourseId.ShouldBe(courseC.Id);
        steps[2].IsCompleted.ShouldBeFalse();
        steps[2].IsLocked.ShouldBeTrue();
        steps[2].IsNext.ShouldBeFalse();
    }

    // --- Admin CRUD ---

    [Fact]
    public async Task CreateAsync_PersistsUnpublishedPath()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var result = await NewService(db).CreateAsync(new SaveLearningPathRequest("New Path", null, null, null, null, 1));

        result.TitleEn.ShouldBe("New Path");
        result.IsPublished.ShouldBeFalse();
        result.Steps.ShouldBeEmpty();
        (await db.LearningPaths.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).UpdateAsync(Guid.NewGuid(), new SaveLearningPathRequest("X", null, null, null, null, 1)))
            .ShouldBeNull();
    }

    [Fact]
    public async Task PublishAsync_SetsIsPublishedAndPublishedAt()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var path = new LearningPath { Id = Guid.NewGuid(), TitleEn = "P" };
        db.LearningPaths.Add(path);
        await db.SaveChangesAsync();

        (await NewService(db).PublishAsync(path.Id)).ShouldBeTrue();

        var reloaded = await db.LearningPaths.FindAsync(path.Id);
        reloaded!.IsPublished.ShouldBeTrue();
        reloaded.PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var path = new LearningPath { Id = Guid.NewGuid(), TitleEn = "P" };
        db.LearningPaths.Add(path);
        await db.SaveChangesAsync();

        (await NewService(db).DeleteAsync(path.Id)).ShouldBeTrue();

        (await db.LearningPaths.CountAsync()).ShouldBe(0); // hidden by the soft-delete query filter
    }

    // --- Step management ---

    [Fact]
    public async Task AddStepAsync_AppendsAtEnd()
    {
        var (db, path, _, _, _) = await SeedPublishedPathAsync();
        await using var _ = db;
        var courseD = NewCourse("Course D");
        db.Courses.Add(courseD);
        await db.SaveChangesAsync();

        var result = await NewService(db).AddStepAsync(path.Id, courseD.Id);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Steps.Count.ShouldBe(4);
        result.Value.Steps.Single(s => s.CourseId == courseD.Id).StepOrder.ShouldBe(4);
    }

    [Fact]
    public async Task AddStepAsync_CourseAlreadyInPath_ReturnsConflict()
    {
        var (db, path, courseA, _, _) = await SeedPublishedPathAsync();
        await using var _ = db;

        var result = await NewService(db).AddStepAsync(path.Id, courseA.Id);

        result.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task AddStepAsync_UnknownCourse_ReturnsBadRequest()
    {
        var (db, path, _, _, _) = await SeedPublishedPathAsync();
        await using var _ = db;

        var result = await NewService(db).AddStepAsync(path.Id, Guid.NewGuid());

        result.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RemoveStepAsync_ReSequencesRemainingSteps()
    {
        var (db, path, courseA, courseB, courseC) = await SeedPublishedPathAsync();
        await using var _ = db;
        var middleStepId = await db.LearningPathSteps.Where(s => s.CourseId == courseB.Id).Select(s => s.Id).SingleAsync();

        (await NewService(db).RemoveStepAsync(path.Id, middleStepId)).ShouldBeTrue();

        var remaining = await db.LearningPathSteps.Where(s => s.LearningPathId == path.Id).OrderBy(s => s.StepOrder).ToListAsync();
        remaining.Count.ShouldBe(2);
        remaining[0].CourseId.ShouldBe(courseA.Id);
        remaining[0].StepOrder.ShouldBe(1);
        remaining[1].CourseId.ShouldBe(courseC.Id);
        remaining[1].StepOrder.ShouldBe(2); // closed the gap left at position 2
    }

    [Fact]
    public async Task MoveStepUpAsync_SwapsWithPrevious()
    {
        var (db, path, courseA, courseB, _) = await SeedPublishedPathAsync();
        await using var _ = db;
        var secondStepId = await db.LearningPathSteps.Where(s => s.CourseId == courseB.Id).Select(s => s.Id).SingleAsync();

        (await NewService(db).MoveStepUpAsync(path.Id, secondStepId)).ShouldBeTrue();

        var steps = await db.LearningPathSteps.Where(s => s.LearningPathId == path.Id).OrderBy(s => s.StepOrder).ToListAsync();
        steps[0].CourseId.ShouldBe(courseB.Id);
        steps[1].CourseId.ShouldBe(courseA.Id);
    }

    [Fact]
    public async Task MoveStepUpAsync_AlreadyFirst_ReturnsFalse()
    {
        var (db, path, courseA, _, _) = await SeedPublishedPathAsync();
        await using var _ = db;
        var firstStepId = await db.LearningPathSteps.Where(s => s.CourseId == courseA.Id).Select(s => s.Id).SingleAsync();

        (await NewService(db).MoveStepUpAsync(path.Id, firstStepId)).ShouldBeFalse();
    }

    [Fact]
    public async Task MoveStepDownAsync_AlreadyLast_ReturnsFalse()
    {
        var (db, path, _, _, courseC) = await SeedPublishedPathAsync();
        await using var _ = db;
        var lastStepId = await db.LearningPathSteps.Where(s => s.CourseId == courseC.Id).Select(s => s.Id).SingleAsync();

        (await NewService(db).MoveStepDownAsync(path.Id, lastStepId)).ShouldBeFalse();
    }
}
