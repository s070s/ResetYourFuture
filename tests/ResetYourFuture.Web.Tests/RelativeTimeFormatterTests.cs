using System.Globalization;
using ResetYourFuture.Web.Extensions;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class RelativeTimeFormatterTests
{
    public RelativeTimeFormatterTests()
    {
        // Pin the UI culture so the neutral (English) resources resolve regardless of runner locale.
        CultureInfo.CurrentUICulture = new CultureInfo("en");
    }

    [Fact]
    public void LastSeen_UnderOneMinute_SaysJustNow()
    {
        var result = RelativeTimeFormatter.LastSeen(DateTime.UtcNow.AddSeconds(-30));

        result.ShouldBe("last seen just now");
    }

    [Fact]
    public void LastSeen_UnderOneHour_SaysMinutesAgo()
    {
        var result = RelativeTimeFormatter.LastSeen(DateTime.UtcNow.AddMinutes(-30));

        result.ShouldBe("last seen 30 min ago");
    }

    [Fact]
    public void LastSeen_UnderOneDay_SaysHoursAgo()
    {
        var result = RelativeTimeFormatter.LastSeen(DateTime.UtcNow.AddHours(-5));

        result.ShouldBe("last seen 5 h ago");
    }

    [Fact]
    public void LastSeen_UpToSevenDays_SaysDaysAgo()
    {
        var result = RelativeTimeFormatter.LastSeen(DateTime.UtcNow.AddDays(-3));

        result.ShouldBe("last seen 3 d ago");
    }

    [Fact]
    public void LastSeen_OlderThanSevenDays_FallsBackToDate()
    {
        var lastSeen = DateTime.UtcNow.AddDays(-30);

        var result = RelativeTimeFormatter.LastSeen(lastSeen);

        result.ShouldBe($"last seen {lastSeen.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)}");
    }
}
