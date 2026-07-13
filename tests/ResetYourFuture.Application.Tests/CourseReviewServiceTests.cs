using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class CourseReviewServiceTests
{
    private const string UserId = "student-1";
    private const string OtherUserId = "student-2";

    private static CourseReviewService NewService(ApplicationDbContext db, INotificationDispatcher? notifications = null) =>
        new(db, notifications ?? Substitute.For<INotificationDispatcher>(), NullLogger<CourseReviewService>.Instance);

    private static ApplicationUser AppUser(string id, string? displayName = null) => new()
    {
        Id = id, UserName = id, Email = $"{id}@x.com", FirstName = "First", LastName = "Last", DisplayName = displayName
    };

    private static Course NewCourse(string title) => new() { Id = Guid.NewGuid(), TitleEn = title, IsPublished = true };

    [Fact]
    public async Task SaveMyReviewAsync_NotEnrolled_ReturnsForbidden()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var result = await NewService(db).SaveMyReviewAsync(UserId, course.Id, new SaveCourseReviewRequest(5, "Great!"));

        result.StatusCode.ShouldBe(403);
        (await db.CourseReviews.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SaveMyReviewAsync_Enrolled_CreatesPendingReview()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id });
        await db.SaveChangesAsync();

        var result = await NewService(db).SaveMyReviewAsync(UserId, course.Id, new SaveCourseReviewRequest(4, "Good course"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe("Pending");
        result.Value.Rating.ShouldBe(4);
        (await db.CourseReviews.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task SaveMyReviewAsync_EditingApprovedReview_ResetsToPending()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id });
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 5, Body = "old", Status = ReviewStatus.Approved });
        await db.SaveChangesAsync();

        var result = await NewService(db).SaveMyReviewAsync(UserId, course.Id, new SaveCourseReviewRequest(2, "changed my mind"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe("Pending");
        result.Value.Rating.ShouldBe(2);
        result.Value.Body.ShouldBe("changed my mind");
        (await db.CourseReviews.CountAsync()).ShouldBe(1); // upsert, not a second row
    }

    [Fact]
    public async Task GetApprovedForCourseAsync_ExcludesPendingAndRejected()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        db.Users.AddRange(AppUser(UserId, "Approved Author"), AppUser(OtherUserId));
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 5, Body = "great", Status = ReviewStatus.Approved });
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = OtherUserId, Rating = 3, Body = "pending", Status = ReviewStatus.Pending });
        await db.SaveChangesAsync();

        var approved = await NewService(db).GetApprovedForCourseAsync(course.Id);

        var review = approved.ShouldHaveSingleItem();
        review.AuthorName.ShouldBe("Approved Author");
        review.Body.ShouldBe("great");
    }

    [Fact]
    public async Task GetApprovedForCourseAsync_NoDisplayName_FallsBackToFirstLastName()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        db.Users.Add(AppUser(UserId)); // no DisplayName
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 5, Body = "great", Status = ReviewStatus.Approved });
        await db.SaveChangesAsync();

        var approved = await NewService(db).GetApprovedForCourseAsync(course.Id);

        approved.ShouldHaveSingleItem().AuthorName.ShouldBe("First Last");
    }

    [Fact]
    public async Task GetRatingSummaryAsync_AveragesOnlyApproved_RoundedToOneDecimal()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = "u1", Rating = 5, Body = "a", Status = ReviewStatus.Approved });
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = "u2", Rating = 4, Body = "b", Status = ReviewStatus.Approved });
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = "u3", Rating = 1, Body = "c", Status = ReviewStatus.Rejected });
        await db.SaveChangesAsync();

        var summary = await NewService(db).GetRatingSummaryAsync(course.Id);

        summary.ShouldNotBeNull();
        summary!.AverageRating.ShouldBe(4.5);
        summary.ReviewCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetRatingSummaryAsync_NoApprovedReviews_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = "u1", Rating = 5, Body = "a", Status = ReviewStatus.Pending });
        await db.SaveChangesAsync();

        (await NewService(db).GetRatingSummaryAsync(course.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task GetRatingSummariesAsync_BatchesAcrossMultipleCourses()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var courseA = NewCourse("A");
        var courseB = NewCourse("B");
        db.Courses.AddRange(courseA, courseB);
        db.CourseReviews.Add(new CourseReview { CourseId = courseA.Id, UserId = "u1", Rating = 5, Body = "a", Status = ReviewStatus.Approved });
        db.CourseReviews.Add(new CourseReview { CourseId = courseB.Id, UserId = "u2", Rating = 3, Body = "b", Status = ReviewStatus.Approved });
        await db.SaveChangesAsync();

        var summaries = await NewService(db).GetRatingSummariesAsync([courseA.Id, courseB.Id, Guid.NewGuid()]);

        summaries.Count.ShouldBe(2);
        summaries[courseA.Id].AverageRating.ShouldBe(5.0);
        summaries[courseB.Id].AverageRating.ShouldBe(3.0);
    }

    [Fact]
    public async Task GetMyReviewAsync_ReturnsOwnReviewAtAnyStatus()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 2, Body = "meh", Status = ReviewStatus.Rejected });
        await db.SaveChangesAsync();

        var mine = await NewService(db).GetMyReviewAsync(UserId, course.Id);

        mine.ShouldNotBeNull();
        mine!.Status.ShouldBe("Rejected");
    }

    [Fact]
    public async Task GetMyReviewAsync_NoReview_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).GetMyReviewAsync(UserId, Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task ApproveAsync_FlipsStatus_AndDispatchesNotification()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("Career Basics");
        db.Courses.Add(course);
        var review = new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 5, Body = "great", Status = ReviewStatus.Pending };
        db.CourseReviews.Add(review);
        await db.SaveChangesAsync();
        var notifications = Substitute.For<INotificationDispatcher>();

        var result = await NewService(db, notifications).ApproveAsync(review.Id);

        result.IsSuccess.ShouldBeTrue();
        (await db.CourseReviews.FindAsync(review.Id))!.Status.ShouldBe(ReviewStatus.Approved);
        await notifications.Received(1).DispatchAsync(
            UserId, NotificationType.ReviewApproved, "ReviewApproved", Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAsync_NotificationDispatchThrows_StillReturnsSuccess()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        var review = new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 5, Body = "great", Status = ReviewStatus.Pending };
        db.CourseReviews.Add(review);
        await db.SaveChangesAsync();
        var notifications = Substitute.For<INotificationDispatcher>();
        notifications.DispatchAsync(Arg.Any<string>(), Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("hub down"));

        var result = await NewService(db, notifications).ApproveAsync(review.Id);

        result.IsSuccess.ShouldBeTrue(); // moderation succeeded even though the best-effort notification failed
        (await db.CourseReviews.FindAsync(review.Id))!.Status.ShouldBe(ReviewStatus.Approved);
    }

    [Fact]
    public async Task ApproveAsync_NotFound_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).ApproveAsync(Guid.NewGuid())).StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task RejectAsync_FlipsStatus()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        var review = new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 1, Body = "bad", Status = ReviewStatus.Pending };
        db.CourseReviews.Add(review);
        await db.SaveChangesAsync();

        var result = await NewService(db).RejectAsync(review.Id);

        result.IsSuccess.ShouldBeTrue();
        (await db.CourseReviews.FindAsync(review.Id))!.Status.ShouldBe(ReviewStatus.Rejected);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByStatus()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("C");
        db.Courses.Add(course);
        db.Users.Add(AppUser(UserId, "Author"));
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 5, Body = "a", Status = ReviewStatus.Pending });
        db.CourseReviews.Add(new CourseReview { CourseId = course.Id, UserId = UserId, Rating = 4, Body = "b", Status = ReviewStatus.Approved });
        await db.SaveChangesAsync();

        var pending = await NewService(db).GetPagedAsync(1, 10, "createdat", "desc", "Pending");
        var all = await NewService(db).GetPagedAsync(1, 10, "createdat", "desc", null);

        pending.TotalCount.ShouldBe(1);
        pending.Items.ShouldHaveSingleItem().Status.ShouldBe("Pending");
        all.TotalCount.ShouldBe(2);
    }
}
