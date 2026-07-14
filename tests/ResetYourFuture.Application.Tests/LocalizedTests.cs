using ResetYourFuture.Application.Common;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class LocalizedTests
{
    [Theory]
    [InlineData("el", true)]
    [InlineData("EL", true)]
    [InlineData("en", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsEl_MatchesCaseInsensitively(string? lang, bool expected)
    {
        Localized.IsEl(lang).ShouldBe(expected);
    }

    [Fact]
    public void Pick_NotEl_ReturnsEnglish()
    {
        Localized.Pick(isEl: false, en: "Hello", el: "Γεια").ShouldBe("Hello");
    }

    [Fact]
    public void Pick_El_WithTranslation_ReturnsGreek()
    {
        Localized.Pick(isEl: true, en: "Hello", el: "Γεια").ShouldBe("Γεια");
    }

    [Fact]
    public void Pick_El_WithoutTranslation_FallsBackToEnglish()
    {
        Localized.Pick(isEl: true, en: "Hello", el: null).ShouldBe("Hello");
    }

    [Fact]
    public void Pick_NonNullEnglish_ReturnsNonNullResult()
    {
        string? result = Localized.Pick(isEl: true, en: "Hello", el: null);

        result.ShouldNotBeNull();
    }
}
