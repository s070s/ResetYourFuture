using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// Verifies ApiClientBase's network resilience (AVAIL-3): a loopback connection failure or timeout
/// must degrade to <c>default</c>/<c>false</c> instead of propagating an exception that would crash
/// the Blazor circuit, idempotent GETs retry a fast-failing connection error, and non-idempotent
/// verbs and timeouts are never retried.
/// </summary>
public class ApiClientResilienceTests
{
    private static ApiTokenProvider TokenProvider()
    {
        var authState = Substitute.For<AuthenticationStateProvider>();
        authState.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
        return new ApiTokenProvider(authState, Substitute.For<IAuthService>());
    }

    // Counts calls and raises the configured exception for the first N calls, then (optionally) succeeds.
    private sealed class FaultHandler(Func<Exception> fault, int failFirst = int.MaxValue) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls <= failFirst)
                return Task.FromException<HttpResponseMessage>(fault());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("\"ok\"", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TestConsumer(HttpClient http, ApiTokenProvider tokens) : ApiClientBase(http, tokens)
    {
        public Task<string?> Get() => GetAsync<string>("api/thing");
        public Task<bool> Post() => ActionAsync("api/thing");
    }

    private static TestConsumer Build(FaultHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new TestConsumer(client, TokenProvider());
    }

    [Fact]
    public async Task Get_ConnectionRefused_DegradesToDefault_AfterRetrying()
    {
        var handler = new FaultHandler(() => new HttpRequestException("Connection refused"));
        var consumer = Build(handler);

        var result = await consumer.Get();

        result.ShouldBeNull();          // degraded, not thrown
        handler.Calls.ShouldBe(3);      // MaxGetAttempts — the two extra are retries
    }

    [Fact]
    public async Task Get_TransientBlipThenSuccess_Recovers()
    {
        var handler = new FaultHandler(() => new HttpRequestException("Connection reset"), failFirst: 1);
        var consumer = Build(handler);

        var result = await consumer.Get();

        result.ShouldBe("ok");
        handler.Calls.ShouldBe(2);      // failed once, retried, succeeded
    }

    [Fact]
    public async Task Get_Timeout_DegradesWithoutRetry()
    {
        // An HttpClient timeout surfaces as TaskCanceledException with no caller cancellation — it must
        // degrade but must NOT be retried (retrying stacks timeouts and may re-issue the request).
        var handler = new FaultHandler(() => new TaskCanceledException("timed out"));
        var consumer = Build(handler);

        var result = await consumer.Get();

        result.ShouldBeNull();
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Post_ConnectionFailure_DegradesToFalse_WithoutRetry()
    {
        var handler = new FaultHandler(() => new HttpRequestException("Connection refused"));
        var consumer = Build(handler);

        var result = await consumer.Post();

        result.ShouldBeFalse();
        handler.Calls.ShouldBe(1);      // non-idempotent — never retried
    }
}
