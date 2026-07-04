using System.Net;
using System.Text;
using System.Text.Json;

namespace ResetYourFuture.TestSupport;

/// <summary>
/// A canned <see cref="HttpMessageHandler"/> for unit-testing typed API consumers without a
/// real server. Records the last request for URL/verb assertions.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_responder(request));
    }
}

/// <summary>Factory helpers for HttpClients backed by <see cref="StubHttpMessageHandler"/>.</summary>
public static class TestHttp
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>An HttpClient that always responds with the given status and (optionally) a JSON body.</summary>
    public static (HttpClient Client, StubHttpMessageHandler Handler) Json(HttpStatusCode status, object? body = null)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(status)
        {
            Content = body is null
                ? new StringContent(string.Empty)
                : new StringContent(JsonSerializer.Serialize(body, Web), Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return (client, handler);
    }
}
