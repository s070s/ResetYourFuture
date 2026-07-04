using Microsoft.Extensions.Logging.Abstractions;
using ResetYourFuture.Infrastructure.ApiServices;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Infrastructure.Tests;

public class StubEmailServiceTests
{
    private static StubEmailService NewService() => new(NullLogger<StubEmailService>.Instance);

    [Fact]
    public async Task SendEmailConfirmation_CompletesWithoutThrowing()
    {
        await Should.NotThrowAsync(() => NewService().SendEmailConfirmationAsync("u@x.com", "https://confirm"));
    }

    [Fact]
    public async Task SendPasswordReset_CompletesWithoutThrowing()
    {
        await Should.NotThrowAsync(() => NewService().SendPasswordResetAsync("u@x.com", "https://reset"));
    }
}
