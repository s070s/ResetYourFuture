using Microsoft.EntityFrameworkCore;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Domain.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="UserSearchExtensions"/> ApplySort / ApplySearch run as EF queries.
/// Exercised against the InMemory provider; string matching there is ordinal
/// (case-sensitive), which is asserted explicitly.
/// </summary>
public class UserSearchExtensionsTests
{
    private static ApplicationUser User(
        string email, string first, string last, DateTime created, bool enabled = true) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email,
            UserName = email,
            FirstName = first,
            LastName = last,
            CreatedAt = created,
            IsEnabled = enabled
        };

    private static async Task<ApplicationDbContext> SeedAsync(params ApplicationUser[] users)
    {
        var db = DbContextFactory.CreateInMemory();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        return db;
    }

    // ---- ApplySort -----------------------------------------------------------

    // Seed: A=a@x.com (Charlie Brown, 2022, enabled),
    //       B=b@x.com (Alice   Young, 2020, disabled),
    //       C=c@x.com (Alice   Adams, 2021, enabled)
    [Theory]
    [InlineData("firstname", "asc", "c,b,a")]
    [InlineData("firstname", "desc", "a,c,b")]
    [InlineData("lastname", "asc", "c,a,b")]
    [InlineData("lastname", "desc", "b,a,c")]
    [InlineData("createdat", "asc", "b,c,a")]
    [InlineData("createdat", "desc", "a,c,b")]
    [InlineData("isenabled", "asc", "b,a,c")]
    [InlineData("isenabled", "desc", "a,c,b")]
    [InlineData("email", "asc", "a,b,c")]
    [InlineData("email", "desc", "c,b,a")]
    [InlineData(null, null, "a,b,c")]
    [InlineData("unknown", "asc", "a,b,c")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            User("a@x.com", "Charlie", "Brown", new DateTime(2022, 1, 1)),
            User("b@x.com", "Alice", "Young", new DateTime(2020, 1, 1), enabled: false),
            User("c@x.com", "Alice", "Adams", new DateTime(2021, 1, 1)));

        var letters = await db.Users
            .ApplySort(sortBy, sortDir)
            .Select(u => u.Email!.Substring(0, 1))
            .ToListAsync();

        string.Join(",", letters).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualNames_FallsBackToEmailTieBreaker()
    {
        await using var db = await SeedAsync(
            User("y@x.com", "Sam", "Lee", new DateTime(2020, 1, 1)),
            User("x@x.com", "Sam", "Lee", new DateTime(2020, 1, 1)));

        var emails = await db.Users
            .ApplySort("firstname", "asc")
            .Select(u => u.Email!)
            .ToListAsync();

        emails.ShouldBe(new[] { "x@x.com", "y@x.com" });
    }

    // ---- ApplySearch: email mode ('@' present) -------------------------------

    [Fact]
    public async Task ApplySearch_EmailPrefixAndSuffix_MatchesBoth()
    {
        await using var db = await SeedEmailUsers();

        var emails = await Search(db, "al@example");

        emails.ShouldBe(new[] { "alice@example.com" });
    }

    [Fact]
    public async Task ApplySearch_EmailPrefixOnly_MatchesStartsWith()
    {
        await using var db = await SeedEmailUsers();

        var emails = await Search(db, "al@");

        emails.ShouldBe(new[] { "alan@test.com", "alice@example.com" });
    }

    [Fact]
    public async Task ApplySearch_EmailSuffixOnly_MatchesDomain()
    {
        await using var db = await SeedEmailUsers();

        var emails = await Search(db, "@example");

        emails.ShouldBe(new[] { "alice@example.com", "bob@example.com" });
    }

    [Fact]
    public async Task ApplySearch_BareAtSign_ReturnsAllUnfiltered()
    {
        await using var db = await SeedEmailUsers();

        var emails = await Search(db, "@");

        emails.Count.ShouldBe(3);
    }

    // ---- ApplySearch: name mode (space present) ------------------------------

    [Theory]
    [InlineData("Alice Adams")]
    [InlineData("Adams Alice")] // reversed order also matches
    public async Task ApplySearch_TwoTokens_MatchesFirstAndLastEitherOrder(string term)
    {
        await using var db = await SeedEmailUsers();

        var emails = await Search(db, term);

        emails.ShouldBe(new[] { "alice@example.com" });
    }

    // ---- ApplySearch: single token -------------------------------------------

    [Fact]
    public async Task ApplySearch_SingleToken_MatchesEmailFirstOrLastContains()
    {
        await using var db = await SeedEmailUsers();

        var emails = await Search(db, "bob");

        emails.ShouldBe(new[] { "bob@example.com" });
    }

    [Fact]
    public async Task ApplySearch_NoMatch_ReturnsEmpty()
    {
        await using var db = await SeedEmailUsers();

        (await Search(db, "zzz")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplySearch_IsCaseSensitiveOnInMemoryProvider()
    {
        await using var db = await SeedAsync(
            User("m.p@contoso.com", "Maria", "Papa", new DateTime(2020, 1, 1)));

        (await Search(db, "maria")).ShouldBeEmpty();     // lower-case: no match
        (await Search(db, "Maria")).Count.ShouldBe(1); // exact case: matches FirstName
    }

    // ---- helpers -------------------------------------------------------------

    private static Task<ApplicationDbContext> SeedEmailUsers() => SeedAsync(
        User("alice@example.com", "Alice", "Adams", new DateTime(2020, 1, 1)),
        User("alan@test.com", "Alan", "Tester", new DateTime(2020, 1, 1)),
        User("bob@example.com", "Bob", "Brown", new DateTime(2020, 1, 1)));

    private static async Task<List<string>> Search(ApplicationDbContext db, string term) =>
        await db.Users.ApplySearch(term).OrderBy(u => u.Email).Select(u => u.Email!).ToListAsync();
}
