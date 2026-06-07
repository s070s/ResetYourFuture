using ResetYourFuture.Web.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="ApplicationUser.Age"/> is computed from <see cref="ApplicationUser.DateOfBirth"/>
/// against the current UTC date. Cases are constructed relative to "today" so they are
/// deterministic on any run date (including year boundaries and leap days, which
/// <see cref="DateOnly.AddYears"/> normalises).
/// </summary>
public class ApplicationUserTests
{
    private static ApplicationUser WithDob( DateOnly? dob ) =>
        new() { FirstName = "Test", LastName = "User", DateOfBirth = dob };

    private static DateOnly Today => DateOnly.FromDateTime( DateTime.UtcNow );

    [Fact]
    public void Age_NullDateOfBirth_ReturnsNull()
    {
        WithDob( null ).Age.ShouldBeNull();
    }

    [Fact]
    public void Age_BirthdayIsToday_ReturnsExactYears()
    {
        WithDob( Today.AddYears( -25 ) ).Age.ShouldBe( 25 );
    }

    [Fact]
    public void Age_BirthdayWasYesterday_ReturnsExactYears()
    {
        WithDob( Today.AddYears( -25 ).AddDays( -1 ) ).Age.ShouldBe( 25 );
    }

    [Fact]
    public void Age_BirthdayIsTomorrow_ReturnsYearsMinusOne()
    {
        WithDob( Today.AddYears( -25 ).AddDays( 1 ) ).Age.ShouldBe( 24 );
    }

    [Fact]
    public void Age_YoungChild_ReturnsYears()
    {
        WithDob( Today.AddYears( -5 ) ).Age.ShouldBe( 5 );
    }
}
