using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using ResetYourFuture.Web.Hubs;
using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// NotificationHub connections are established from server-side Blazor circuit code
/// (HubConnectionBuilder running in NotificationBell's C#), so they're invisible to
/// browser-side network inspection — this is what actually proves the connect/disconnect
/// contract the rest of the feature (ChatHub's online check, NotificationDispatcher's push)
/// depends on.
/// </summary>
public class NotificationHubTests
{
    private const string UserId = "user-1";

    private sealed class Harness
    {
        public required NotificationHub Hub;
        public required HubCallerContext Context;
        public required IGroupManager Groups;
        public required NotificationConnectionTracker Tracker;
    }

    private static Harness Build(string? userId)
    {
        var context = Substitute.For<HubCallerContext>();
        context.UserIdentifier.Returns(userId);
        context.ConnectionId.Returns("conn-1");

        var groups = Substitute.For<IGroupManager>();
        var tracker = new NotificationConnectionTracker();

        var hub = new NotificationHub(tracker) { Context = context, Groups = groups };

        return new Harness { Hub = hub, Context = context, Groups = groups, Tracker = tracker };
    }

    [Fact]
    public async Task OnConnected_JoinsUserGroup_AndMarksOnline()
    {
        var h = Build(UserId);

        await h.Hub.OnConnectedAsync();

        await h.Groups.Received(1).AddToGroupAsync("conn-1", $"user_{UserId}", Arg.Any<CancellationToken>());
        h.Tracker.IsOnline(UserId).ShouldBeTrue();
    }

    [Fact]
    public async Task OnConnected_AnonymousContext_NoOp()
    {
        var h = Build(null);

        await h.Hub.OnConnectedAsync();

        await h.Groups.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnDisconnected_LeavesGroup_AndMarksOffline()
    {
        var h = Build(UserId);
        await h.Hub.OnConnectedAsync();

        await h.Hub.OnDisconnectedAsync(null);

        await h.Groups.Received(1).RemoveFromGroupAsync("conn-1", $"user_{UserId}", Arg.Any<CancellationToken>());
        h.Tracker.IsOnline(UserId).ShouldBeFalse();
    }

    [Fact]
    public async Task MultipleConnections_SameUser_StaysOnlineUntilLastDisconnects()
    {
        var tracker = new NotificationConnectionTracker();
        var first = Build(UserId);
        var second = Build(UserId);
        // Share one tracker across both "tabs" for this user.
        var hub1 = new NotificationHub(tracker) { Context = first.Context, Groups = first.Groups };
        var hub2 = new NotificationHub(tracker) { Context = second.Context, Groups = second.Groups };

        await hub1.OnConnectedAsync();
        await hub2.OnConnectedAsync();
        tracker.IsOnline(UserId).ShouldBeTrue();

        await hub1.OnDisconnectedAsync(null);
        tracker.IsOnline(UserId).ShouldBeTrue("second tab is still connected");

        await hub2.OnDisconnectedAsync(null);
        tracker.IsOnline(UserId).ShouldBeFalse();
    }
}
