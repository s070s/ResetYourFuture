using ResetYourFuture.Web.Startup;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class ServiceRegistrationExtensionsTests
{
    [Fact]
    public void ResolveSelfBaseUrl_Development_DefaultsToLocalhost_WhenUnset()
    {
        var result = ServiceRegistrationExtensions.ResolveSelfBaseUrl(null, isDevelopment: true);

        result.ShouldBe("https://localhost:7090");
    }

    [Fact]
    public void ResolveSelfBaseUrl_Development_UsesConfiguredValue_WhenSet()
    {
        var result = ServiceRegistrationExtensions.ResolveSelfBaseUrl("https://dev.example", isDevelopment: true);

        result.ShouldBe("https://dev.example");
    }

    [Fact]
    public void ResolveSelfBaseUrl_NonDevelopment_ReturnsConfiguredValue()
    {
        var result = ServiceRegistrationExtensions.ResolveSelfBaseUrl("https://reset-your-future.com", isDevelopment: false);

        result.ShouldBe("https://reset-your-future.com");
    }

    [Fact]
    public void ResolveSelfBaseUrl_NonDevelopment_ThrowsWhenUnset()
    {
        Should.Throw<InvalidOperationException>(() => ServiceRegistrationExtensions.ResolveSelfBaseUrl(null, isDevelopment: false));
    }

    [Fact]
    public void ResolveSelfBaseUrl_NonDevelopment_ThrowsWhenBlank()
    {
        Should.Throw<InvalidOperationException>(() => ServiceRegistrationExtensions.ResolveSelfBaseUrl("   ", isDevelopment: false));
    }

    [Fact]
    public void ResolveSelfBaseUrl_NonDevelopment_ThrowsWhenStillPointsAtLocalhost()
    {
        Should.Throw<InvalidOperationException>(() => ServiceRegistrationExtensions.ResolveSelfBaseUrl("https://localhost:7090", isDevelopment: false));
    }
}
