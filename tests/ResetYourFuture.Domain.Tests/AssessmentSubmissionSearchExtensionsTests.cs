using Microsoft.EntityFrameworkCore;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Domain.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="AssessmentSubmissionSearchExtensions.ApplySort"/> run as EF queries
/// against the InMemory provider, plus a ToQueryString guard for SQL Server
/// translatability. Submissions get fixed ascending Ids so ThenBy(Id) tie-breaks
/// are deterministic.
/// </summary>
public class AssessmentSubmissionSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static ApplicationUser User(string id, string first, string last) => new()
    {
        Id = id,
        Email = $"{id}@x.com",
        UserName = $"{id}@x.com",
        FirstName = first,
        LastName = last
    };

    private static AssessmentSubmission Submission(
        int id, ApplicationUser user, AssessmentDefinition definition, int submittedYear) =>
        new()
        {
            Id = FixedId(id),
            UserId = user.Id,
            User = user,
            AssessmentDefinitionId = definition.Id,
            AssessmentDefinition = definition,
            AnswersJson = "{}",
            SubmittedAt = new DateTimeOffset(submittedYear, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

    private static AssessmentDefinition Definition(string title, string? categoryName = null) => new()
    {
        Id = Guid.NewGuid(),
        TitleEn = title,
        Key = title.ToLowerInvariant(),
        SchemaJson = "{}",
        Category = categoryName is null ? null : new Category { Id = Guid.NewGuid(), NameEn = categoryName }
    };

    // Seed (Ids ascending A<B<C):
    //   A: user Zoe Adams  (a@x),  def "Mindset"  cat "Zeta",  2020
    //   B: user Ann Brown  (b@x),  def "Career"   cat "Echo",  2022
    //   C: user Ann Adams  (c@x),  def "Skills"   no category, 2021
    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var db = DbContextFactory.CreateInMemory();
        var zoe = User("a-user", "Zoe", "Adams");
        var annB = User("b-user", "Ann", "Brown");
        var annA = User("c-user", "Ann", "Adams");
        db.AssessmentSubmissions.AddRange(
            Submission(1, zoe, Definition("Mindset", "Zeta"), 2020),
            Submission(2, annB, Definition("Career", "Echo"), 2022),
            Submission(3, annA, Definition("Skills"), 2021));
        await db.SaveChangesAsync();
        return db;
    }

    [Theory]
    [InlineData("user", "asc", "3,1,2")]  // LastName then FirstName: (Adams,Ann), (Adams,Zoe), (Brown,Ann)
    [InlineData("user", "desc", "2,1,3")]
    [InlineData("email", "asc", "1,2,3")]
    [InlineData("email", "desc", "3,2,1")]
    [InlineData("title", "asc", "2,1,3")]
    [InlineData("title", "desc", "3,1,2")]
    [InlineData("category", "asc", "3,2,1")] // null category first
    [InlineData("category", "desc", "1,2,3")]
    [InlineData("submittedat", "asc", "1,3,2")]
    [InlineData("submittedat", "desc", "2,3,1")]
    [InlineData(null, null, "2,3,1")]        // default = submittedat desc (pre-sorting behavior)
    [InlineData("unknown", "asc", "2,3,1")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync();

        var ids = await db.AssessmentSubmissions
            .ApplySort(sortBy, sortDir)
            .Select(s => s.Id)
            .ToListAsync();

        string.Join(",", ids.Select(id => id.ToString("N").TrimStart('0'))).ShouldBe(expectedCsv);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("email")]
    [InlineData("title")]
    [InlineData("category")]
    [InlineData("submittedat")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.AssessmentSubmissions.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
