using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.ApiServices;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Domain.Enums;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class CourseServiceTests
{
    private const string UserId = "user-1";

    private static UserSubscriptionStatusDto Status(
        SubscriptionTierEnum tier = SubscriptionTierEnum.Free,
        int maxCourses = int.MaxValue,
        bool certificateAccess = false ) =>
        new( tier, tier.ToString(), DateTime.UtcNow, null, true,
            new PlanFeaturesDto { MaxCourses = maxCourses, CertificateAccess = certificateAccess } );

    private static (CourseService svc, ISubscriptionService subs, ICertificateService certs) NewService(
        ApplicationDbContext db, UserSubscriptionStatusDto? status = null )
    {
        var subs = Substitute.For<ISubscriptionService>();
        subs.GetUserStatusAsync( Arg.Any<string>(), Arg.Any<CancellationToken>() )
            .Returns( status ?? Status() );
        var certs = Substitute.For<ICertificateService>();
        var svc = new CourseService( db, subs, certs, NullLogger<CourseService>.Instance );
        return (svc, subs, certs);
    }

    private static Course CourseWithLessons(
        string titleEn, int lessonCount, bool published = true,
        SubscriptionTierEnum tier = SubscriptionTierEnum.Free, string? titleEl = null )
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            TitleEn = titleEn,
            TitleEl = titleEl,
            IsPublished = published,
            RequiredTier = tier
        };
        var module = new Module { Id = Guid.NewGuid(), TitleEn = "M1", SortOrder = 1, CourseId = course.Id };
        for ( var i = 0; i < lessonCount; i++ )
        {
            module.Lessons.Add( new Lesson
            {
                Id = Guid.NewGuid(),
                TitleEn = $"L{i + 1}",
                SortOrder = i + 1,
                DurationMinutes = 10,
                ContentEn = $"content-{i + 1}"
            } );
        }
        course.Modules.Add( module );
        return course;
    }

    // ---- GetPublishedCoursesAsync -------------------------------------------

    [Fact]
    public async Task GetPublishedCourses_ReturnsOnlyPublished_OrderedByTitle()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add( CourseWithLessons( "Beta", 0 ) );
        db.Courses.Add( CourseWithLessons( "Alpha", 0 ) );
        db.Courses.Add( CourseWithLessons( "Hidden", 0, published: false ) );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        var result = await svc.GetPublishedCoursesAsync( UserId, 1, 10, "en" );

        result.TotalCount.ShouldBe( 2 );
        result.Items.Select( i => i.Title ).ShouldBe( new[] { "Alpha", "Beta" } );
    }

    [Fact]
    public async Task GetPublishedCourses_SetsEnrolledFlagAndLessonCount()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "Course", 3 );
        db.Courses.Add( course );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id } );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        var item = ( await svc.GetPublishedCoursesAsync( UserId, 1, 10, "en" ) ).Items.Single();

        item.IsEnrolled.ShouldBeTrue();
        item.TotalLessons.ShouldBe( 3 );
    }

    [Fact]
    public async Task GetPublishedCourses_GreekFallsBackToEnglishWhenElNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add( CourseWithLessons( "EnTitle", 0, titleEl: null ) );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        ( await svc.GetPublishedCoursesAsync( UserId, 1, 10, "el" ) ).Items.Single().Title.ShouldBe( "EnTitle" );
    }

    [Fact]
    public async Task GetPublishedCourses_PaginatesWithCorrectTotal()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add( CourseWithLessons( "A", 0 ) );
        db.Courses.Add( CourseWithLessons( "B", 0 ) );
        db.Courses.Add( CourseWithLessons( "C", 0 ) );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        var page2 = await svc.GetPublishedCoursesAsync( UserId, 2, 1, "en" );

        page2.TotalCount.ShouldBe( 3 );
        page2.Items.Single().Title.ShouldBe( "B" );
    }

    // ---- GetCourseDetailAsync -----------------------------------------------

    [Fact]
    public async Task GetCourseDetail_Missing_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService( db );

        ( await svc.GetCourseDetailAsync( UserId, Guid.NewGuid(), "en" ) ).ShouldBeNull();
    }

    [Fact]
    public async Task GetCourseDetail_Unpublished_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 2, published: false );
        db.Courses.Add( course );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        ( await svc.GetCourseDetailAsync( UserId, course.Id, "en" ) ).ShouldBeNull();
    }

    [Fact]
    public async Task GetCourseDetail_ComputesProgressPercent()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 4 );
        db.Courses.Add( course );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id } );
        var firstLesson = course.Modules.First().Lessons.First();
        db.LessonCompletions.Add( new LessonCompletion { Id = Guid.NewGuid(), UserId = UserId, LessonId = firstLesson.Id } );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        var detail = await svc.GetCourseDetailAsync( UserId, course.Id, "en" );

        detail!.TotalLessons.ShouldBe( 4 );
        detail.CompletedLessons.ShouldBe( 1 );
        detail.ProgressPercent.ShouldBe( 25.0 );
        detail.IsEnrolled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCourseDetail_NoLessons_ProgressIsZero()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 0 );
        db.Courses.Add( course );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        ( await svc.GetCourseDetailAsync( UserId, course.Id, "en" ) )!.ProgressPercent.ShouldBe( 0 );
    }

    // ---- EnrollAsync ---------------------------------------------------------

    [Fact]
    public async Task Enroll_CourseMissing_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService( db );

        var result = await svc.EnrollAsync( UserId, Guid.NewGuid() );

        result.StatusCode.ShouldBe( 404 );
    }

    [Fact]
    public async Task Enroll_TierBelowRequired_ReturnsForbidden()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "Pro", 0, tier: SubscriptionTierEnum.Pro );
        db.Courses.Add( course );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db, Status( SubscriptionTierEnum.Free ) );

        ( await svc.EnrollAsync( UserId, course.Id ) ).StatusCode.ShouldBe( 403 );
    }

    [Fact]
    public async Task Enroll_AtMaxCoursesCap_ReturnsForbidden()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var existing = CourseWithLessons( "Existing", 0 );
        var target = CourseWithLessons( "Target", 0 );
        db.Courses.AddRange( existing, target );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = existing.Id } );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db, Status( SubscriptionTierEnum.Free, maxCourses: 1 ) );

        ( await svc.EnrollAsync( UserId, target.Id ) ).StatusCode.ShouldBe( 403 );
    }

    [Fact]
    public async Task Enroll_UnlimitedPlan_BypassesCap()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var existing = CourseWithLessons( "Existing", 0 );
        var target = CourseWithLessons( "Target", 0 );
        db.Courses.AddRange( existing, target );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = existing.Id } );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db, Status( maxCourses: int.MaxValue ) );

        ( await svc.EnrollAsync( UserId, target.Id ) ).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Enroll_AlreadyEnrolled_ReturnsOkWithoutDuplicate()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 0 );
        db.Courses.Add( course );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id } );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        var result = await svc.EnrollAsync( UserId, course.Id );

        result.IsSuccess.ShouldBeTrue();
        ( await db.Enrollments.CountAsync( e => e.UserId == UserId && e.CourseId == course.Id ) ).ShouldBe( 1 );
    }

    [Fact]
    public async Task Enroll_NewEnrollment_PersistsActive()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 0 );
        db.Courses.Add( course );
        await db.SaveChangesAsync();
        var (svc, _, _) = NewService( db );

        var result = await svc.EnrollAsync( UserId, course.Id );

        result.IsSuccess.ShouldBeTrue();
        var enrollment = await db.Enrollments.SingleAsync( e => e.UserId == UserId && e.CourseId == course.Id );
        enrollment.Status.ShouldBe( EnrollmentStatus.Active );
    }

    // ---- GetLessonDetailAsync ------------------------------------------------

    [Fact]
    public async Task GetLessonDetail_Missing_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (svc, _, _) = NewService( db );

        ( await svc.GetLessonDetailAsync( UserId, Guid.NewGuid(), "en" ) ).StatusCode.ShouldBe( 404 );
    }

    [Fact]
    public async Task GetLessonDetail_NotEnrolled_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 2 );
        db.Courses.Add( course );
        await db.SaveChangesAsync();
        var lessonId = course.Modules.First().Lessons.First().Id;
        var (svc, _, _) = NewService( db );

        ( await svc.GetLessonDetailAsync( UserId, lessonId, "en" ) ).StatusCode.ShouldBe( 400 );
    }

    [Fact]
    public async Task GetLessonDetail_ComputesPreviousAndNextNeighbors()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 3 );
        db.Courses.Add( course );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id } );
        await db.SaveChangesAsync();
        var lessons = course.Modules.First().Lessons.OrderBy( l => l.SortOrder ).ToList();
        var (svc, _, _) = NewService( db );

        var middle = ( await svc.GetLessonDetailAsync( UserId, lessons[1].Id, "en" ) ).Value!;
        middle.PreviousLessonId.ShouldBe( lessons[0].Id );
        middle.NextLessonId.ShouldBe( lessons[2].Id );

        var first = ( await svc.GetLessonDetailAsync( UserId, lessons[0].Id, "en" ) ).Value!;
        first.PreviousLessonId.ShouldBeNull();

        var last = ( await svc.GetLessonDetailAsync( UserId, lessons[2].Id, "en" ) ).Value!;
        last.NextLessonId.ShouldBeNull();
    }

    // ---- CompleteLessonAsync -------------------------------------------------

    [Fact]
    public async Task CompleteLesson_NotEnrolled_ReturnsBadRequest()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 1 );
        db.Courses.Add( course );
        await db.SaveChangesAsync();
        var lessonId = course.Modules.First().Lessons.First().Id;
        var (svc, _, _) = NewService( db );

        ( await svc.CompleteLessonAsync( UserId, lessonId ) ).StatusCode.ShouldBe( 400 );
    }

    [Fact]
    public async Task CompleteLesson_FirstCompletion_PersistsAndIncrements()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 2 );
        db.Courses.Add( course );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id } );
        await db.SaveChangesAsync();
        var lessonId = course.Modules.First().Lessons.First().Id;
        var (svc, _, _) = NewService( db );

        var result = await svc.CompleteLessonAsync( UserId, lessonId );

        result.Value!.CompletedLessons.ShouldBe( 1 );
        result.Value.CourseCompleted.ShouldBeFalse();
        ( await db.LessonCompletions.CountAsync() ).ShouldBe( 1 );
    }

    [Fact]
    public async Task CompleteLesson_Idempotent_DoesNotDoubleCount()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 2 );
        db.Courses.Add( course );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id } );
        await db.SaveChangesAsync();
        var lessonId = course.Modules.First().Lessons.First().Id;
        var (svc, _, _) = NewService( db );

        await svc.CompleteLessonAsync( UserId, lessonId );
        var second = await svc.CompleteLessonAsync( UserId, lessonId );

        second.Value!.CompletedLessons.ShouldBe( 1 );
        ( await db.LessonCompletions.CountAsync() ).ShouldBe( 1 );
    }

    [Fact]
    public async Task CompleteLesson_FinalLesson_CompletesCourseAndGeneratesCertificate()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 1 );
        db.Courses.Add( course );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id } );
        await db.SaveChangesAsync();
        var lessonId = course.Modules.First().Lessons.First().Id;
        var (svc, _, certs) = NewService( db, Status( certificateAccess: true ) );

        var result = await svc.CompleteLessonAsync( UserId, lessonId );

        result.Value!.CourseCompleted.ShouldBeTrue();
        ( await db.Enrollments.SingleAsync() ).Status.ShouldBe( EnrollmentStatus.Completed );
        await certs.Received( 1 ).GetOrGenerateAsync( UserId, course.Id, Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task CompleteLesson_CertificateFailure_IsSwallowed()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = CourseWithLessons( "C", 1 );
        db.Courses.Add( course );
        db.Enrollments.Add( new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id } );
        await db.SaveChangesAsync();
        var lessonId = course.Modules.First().Lessons.First().Id;
        var (svc, _, certs) = NewService( db, Status( certificateAccess: true ) );
        certs.GetOrGenerateAsync( Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>() )
            .Returns( _ => Task.FromException<Certificate>( new InvalidOperationException( "boom" ) ) );

        var result = await svc.CompleteLessonAsync( UserId, lessonId );

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CourseCompleted.ShouldBeTrue();
    }
}
