using System.Net;

namespace CoffeeTest.Helpers;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/>. Records the last request
/// (method, URI, body) and either returns a canned response or throws a
/// configured exception to simulate network failures / timeouts.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _responder;
    private readonly Exception? _throw;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public int CallCount { get; private set; }

    public StubHttpMessageHandler(HttpStatusCode status, string body = "")
        => _responder = _ => new HttpResponseMessage(status) { Content = new StringContent(body) };

    public StubHttpMessageHandler(Exception toThrow)
        => _throw = toThrow;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        if (_throw is not null)
        {
            throw _throw;
        }

        return _responder!(request);
    }
}
