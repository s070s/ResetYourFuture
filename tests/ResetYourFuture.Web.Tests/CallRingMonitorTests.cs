using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.Hubs;
using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class CallRingMonitorTests
{
    private static CallRingMonitor Build(ApplicationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => db);
        services.AddScoped(_ => Substitute.For<ICallEventService>());
        var provider = services.BuildServiceProvider();

        return new CallRingMonitor(
            provider,
            new CallRegistry(),
            Options.Create(new WebRtcOptions { RingTimeoutSeconds = 45, MaxParticipants = 6 }),
            Substitute.For<IHubContext<CallHub>>(),
            Substitute.For<IHubContext<ChatHub>>(),
            NullLogger<CallRingMonitor>.Instance);
    }

    // ExecuteAsync is protected on the sealed BackgroundService; invoke it directly so the test
    // can await the loop task itself (StartAsync/StopAsync swallow the fault via Task.WhenAny).
    private static Task RunExecute(CallRingMonitor monitor, CancellationToken ct)
    {
        var method = typeof(CallRingMonitor).GetMethod(
            "ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(monitor, [ct])!;
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoppingTokenCancelled_CompletesWithoutThrowing()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var monitor = Build(db);
        using var cts = new CancellationTokenSource();

        var run = RunExecute(monitor, cts.Token);
        // Startup sweep + first poll are near-instant, so cancellation lands while the loop is
        // parked in Task.Delay — the exact Ctrl+C shutdown path.
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        await Should.NotThrowAsync(async () => await run);
        run.IsCompletedSuccessfully.ShouldBeTrue();
    }
}
