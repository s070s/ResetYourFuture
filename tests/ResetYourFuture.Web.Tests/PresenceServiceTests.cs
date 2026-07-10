using NSubstitute;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class PresenceServiceTests
{
    private const string UserA = "user-a";
    private const string UserB = "user-b";

    private static (PresenceService Service, ICallService Calls) NewService(
        bool connected = true, List<string>? online = null)
    {
        var calls = Substitute.For<ICallService>();
        calls.IsConnected.Returns(connected);
        calls.GetOnlineUsersAsync().Returns(online ?? []);
        return (new PresenceService(calls), calls);
    }

    [Fact]
    public void IsOnline_BeforeSeeding_ReturnsFalse()
    {
        var (service, _) = NewService(online: [UserA]);

        service.IsOnline(UserA).ShouldBeFalse();
    }

    [Fact]
    public async Task EnsureSeededAsync_WhenConnected_SeedsSnapshot()
    {
        var (service, _) = NewService(online: [UserA]);

        await service.EnsureSeededAsync();

        service.IsOnline(UserA).ShouldBeTrue();
        service.IsOnline(UserB).ShouldBeFalse();
    }

    [Fact]
    public async Task EnsureSeededAsync_WhenNotConnected_DoesNotSeed()
    {
        var (service, calls) = NewService(connected: false, online: [UserA]);

        await service.EnsureSeededAsync();

        service.IsOnline(UserA).ShouldBeFalse();
        await calls.DidNotReceive().GetOnlineUsersAsync();
    }

    [Fact]
    public async Task EnsureSeededAsync_CalledTwice_FetchesSnapshotOnce()
    {
        var (service, calls) = NewService(online: [UserA]);

        await service.EnsureSeededAsync();
        await service.EnsureSeededAsync();

        await calls.Received(1).GetOnlineUsersAsync();
    }

    [Fact]
    public void PresenceChanged_Online_MarksUserOnlineAndRaisesChanged()
    {
        var (service, calls) = NewService();
        var changedRaised = false;
        service.Changed += () => changedRaised = true;

        calls.PresenceChanged += Raise.Event<Action<string, bool, DateTime?>>(UserA, true, (DateTime?)null);

        service.IsOnline(UserA).ShouldBeTrue();
        changedRaised.ShouldBeTrue();
    }

    [Fact]
    public void PresenceChanged_Offline_MarksUserOfflineAndOverridesLastSeen()
    {
        var (service, calls) = NewService();
        var lastSeen = DateTime.UtcNow;
        calls.PresenceChanged += Raise.Event<Action<string, bool, DateTime?>>(UserA, true, (DateTime?)null);

        calls.PresenceChanged += Raise.Event<Action<string, bool, DateTime?>>(UserA, false, (DateTime?)lastSeen);

        service.IsOnline(UserA).ShouldBeFalse();
        service.GetLastSeen(UserA, fallback: null).ShouldBe(lastSeen);
    }

    [Fact]
    public void GetLastSeen_WithoutLiveOverride_ReturnsFallback()
    {
        var (service, _) = NewService();
        var fallback = DateTime.UtcNow.AddHours(-2);

        service.GetLastSeen(UserA, fallback).ShouldBe(fallback);
    }

    [Fact]
    public async Task StateChanged_ConnectTransition_SeedsSnapshot()
    {
        var (service, calls) = NewService(connected: false, online: [UserA]);
        await service.EnsureSeededAsync();
        service.IsOnline(UserA).ShouldBeFalse();

        calls.IsConnected.Returns(true);
        calls.StateChanged += Raise.Event<Action>();

        service.IsOnline(UserA).ShouldBeTrue();
    }

    [Fact]
    public async Task StateChanged_ReconnectTransition_ReplacesStaleSnapshot()
    {
        var (service, calls) = NewService(online: [UserA]);
        await service.EnsureSeededAsync();

        // Connection drops (events missed), then reconnects with a different online set.
        calls.IsConnected.Returns(false);
        calls.StateChanged += Raise.Event<Action>();
        calls.GetOnlineUsersAsync().Returns([UserB]);
        calls.IsConnected.Returns(true);
        calls.StateChanged += Raise.Event<Action>();

        service.IsOnline(UserA).ShouldBeFalse();
        service.IsOnline(UserB).ShouldBeTrue();
    }

    [Fact]
    public void Dispose_UnsubscribesFromCallServiceEvents()
    {
        var (service, calls) = NewService();

        service.Dispose();
        calls.PresenceChanged += Raise.Event<Action<string, bool, DateTime?>>(UserA, true, (DateTime?)null);

        service.IsOnline(UserA).ShouldBeFalse();
    }
}
