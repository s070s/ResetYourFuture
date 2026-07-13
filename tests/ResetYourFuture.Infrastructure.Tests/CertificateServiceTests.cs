using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Infrastructure.ApiServices;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Infrastructure.Tests;

public class CertificateServiceTests
{
    private static CertificateService NewService(ApplicationDbContext db, IFileStorage storage) =>
        new(db, storage, Substitute.For<INotificationDispatcher>(), NullLogger<CertificateService>.Instance);

    private static async Task<(Course course, string userId)> SeedCompletedAsync(
        ApplicationDbContext db, string? displayName = null, int[]? durations = null)
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            UserName = "u@x.com",
            Email = "u@x.com",
            FirstName = "John",
            LastName = "Doe",
            DisplayName = displayName
        };
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C# Basics", TitleEl = null };
        var module = new Module { Id = Guid.NewGuid(), TitleEn = "M", CourseId = course.Id };
        foreach (var d in durations ?? [])
            module.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TitleEn = "L", ModuleId = module.Id, DurationMinutes = d });
        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CourseId = course.Id,
            Status = EnrollmentStatus.Completed
        };
        db.Users.Add(user);
        db.Courses.Add(course);
        db.Modules.Add(module);
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();
        return (course, user.Id);
    }

    private static Certificate ExistingCert(string userId, Guid courseId, string? pdfPath = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CourseId = courseId,
        EnrollmentId = Guid.NewGuid(),
        RecipientName = "John Doe",
        CourseTitleEn = "C# Basics",
        Status = CertificateStatus.Active,
        PdfPath = pdfPath
    };

    [Fact]
    public async Task GetOrGenerate_New_BuildsPersistsAndStoresPdf()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (course, userId) = await SeedCompletedAsync(db, durations: new[] { 30, 30 });

        byte[]? pdf = null;
        var storage = Substitute.For<IFileStorage>();
        storage.SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                using var ms = new MemoryStream();
                ci.Arg<Stream>().CopyTo(ms);
                pdf = ms.ToArray();
                return "certificates/cert.pdf";
            });

        var cert = await NewService(db, storage).GetOrGenerateAsync(userId, course.Id);

        cert.RecipientName.ShouldBe("John Doe");
        cert.CourseTitleEn.ShouldBe("C# Basics");
        cert.TotalDurationMinutes.ShouldBe(60);
        cert.PdfPath.ShouldBe("certificates/cert.pdf");
        cert.Status.ShouldBe(CertificateStatus.Active);
        (await db.Certificates.CountAsync()).ShouldBe(1);
        pdf.ShouldNotBeNull();
        Encoding.ASCII.GetString(pdf!, 0, 4).ShouldBe("%PDF");
    }

    [Fact]
    public async Task GetOrGenerate_PrefersDisplayName()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (course, userId) = await SeedCompletedAsync(db, displayName: "Johnny D");
        var storage = Substitute.For<IFileStorage>();
        storage.SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns("certificates/cert.pdf");

        var cert = await NewService(db, storage).GetOrGenerateAsync(userId, course.Id);

        cert.RecipientName.ShouldBe("Johnny D");
    }

    [Fact]
    public async Task GetOrGenerate_NoDuration_LeavesTotalNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var (course, userId) = await SeedCompletedAsync(db, durations: []);
        var storage = Substitute.For<IFileStorage>();
        storage.SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns("certificates/cert.pdf");

        var cert = await NewService(db, storage).GetOrGenerateAsync(userId, course.Id);

        cert.TotalDurationMinutes.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrGenerate_Existing_ReturnsItWithoutRegenerating()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var courseId = Guid.NewGuid();
        var existing = ExistingCert("u1", courseId, "certificates/old.pdf");
        db.Certificates.Add(existing);
        await db.SaveChangesAsync();
        var storage = Substitute.For<IFileStorage>();

        var cert = await NewService(db, storage).GetOrGenerateAsync("u1", courseId);

        cert.Id.ShouldBe(existing.Id);
        await storage.DidNotReceive().SaveFileAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("en-GB", "C# Basics", "Βασικά C#", "C# Basics")]   // English culture → English title
    [InlineData("el-GR", "C# Basics", "Βασικά C#", "Βασικά C#")]   // Greek culture → Greek title
    [InlineData("el-GR", "C# Basics", null, "C# Basics")]           // Greek culture, no Greek title → English
    [InlineData("en-GB", "C# Basics", null, "C# Basics")]           // English culture, no Greek title → English
    [InlineData("fr-FR", "C# Basics", "Βασικά C#", "C# Basics")]    // Other culture → English (not Greek)
    public void ResolveCourseTitle_PicksTitleByCulture(string culture, string titleEn, string? titleEl, string expected)
    {
        var result = CertificateService.ResolveCourseTitle(
            titleEn, titleEl, System.Globalization.CultureInfo.GetCultureInfo(culture));

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task GetOrGenerate_NoCompletedEnrollment_Throws()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var storage = Substitute.For<IFileStorage>();

        await Should.ThrowAsync<InvalidOperationException>(
            () => NewService(db, storage).GetOrGenerateAsync("u1", Guid.NewGuid()));
    }

    [Fact]
    public async Task Revoke_SetsRevokedStatusAndTimestamp()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var cert = ExistingCert("u1", Guid.NewGuid());
        db.Certificates.Add(cert);
        await db.SaveChangesAsync();

        await NewService(db, Substitute.For<IFileStorage>()).RevokeAsync(cert.Id);

        var reloaded = await db.Certificates.FindAsync(cert.Id);
        reloaded!.Status.ShouldBe(CertificateStatus.Revoked);
        reloaded.RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Revoke_Missing_Throws()
    {
        await using var db = DbContextFactory.CreateInMemory();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => NewService(db, Substitute.For<IFileStorage>()).RevokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Regenerate_DeletesOldFileAndStoresNew()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var cert = ExistingCert("u1", Guid.NewGuid(), "certificates/old.pdf");
        db.Certificates.Add(cert);
        await db.SaveChangesAsync();
        var storage = Substitute.For<IFileStorage>();
        storage.FileExists("certificates/old.pdf").Returns(true);
        storage.SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns("certificates/new.pdf");

        await NewService(db, storage).RegenerateAsync(cert.Id);

        await storage.Received().DeleteFileAsync("certificates/old.pdf", Arg.Any<CancellationToken>());
        (await db.Certificates.FindAsync(cert.Id))!.PdfPath.ShouldBe("certificates/new.pdf");
    }

    [Fact]
    public async Task Regenerate_Missing_Throws()
    {
        await using var db = DbContextFactory.CreateInMemory();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => NewService(db, Substitute.For<IFileStorage>()).RegenerateAsync(Guid.NewGuid()));
    }
}
