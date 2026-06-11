using System.Net;
using System.Text;
using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Services;

public class HomeConnectServiceTests
{
    private const string WebhookUrl = "https://n8n.test/webhook/coffee-power";

    private static IConfiguration Config(string? url = WebhookUrl, string? user = null, string? password = null)
    {
        var dict = new Dictionary<string, string?>();
        if (url is not null) dict["N8n:PowerWebhookUrl"] = url;
        if (user is not null) dict["N8n:BasicAuthUser"] = user;
        if (password is not null) dict["N8n:BasicAuthPassword"] = password;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static HomeConnectService Create(HttpClient client, IConfiguration? config = null)
        => new(client, NullLogger<HomeConnectService>.Instance, config ?? Config());

    // ─── Constructor ────────────────────────────────────────────

    [Fact]
    public void Constructor_WithoutWebhookUrl_ThrowsInvalidOperation()
    {
        var client = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK));

        Assert.Throws<InvalidOperationException>(() => Create(client, Config(url: null)));
    }

    [Fact]
    public void Constructor_WithBasicAuth_SetsBasicAuthorizationHeader()
    {
        var client = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK));

        _ = Create(client, Config(user: "n8n", password: "s3cret"));

        var auth = client.DefaultRequestHeaders.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Basic", auth!.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("n8n:s3cret")), auth.Parameter);
    }

    [Fact]
    public void Constructor_WithoutBasicAuth_LeavesAuthorizationHeaderUnset()
    {
        var client = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK));

        _ = Create(client);

        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    // ─── SetPowerStateAsync ─────────────────────────────────────

    [Theory]
    [InlineData(true, "on")]
    [InlineData(false, "off")]
    public async Task SetPowerStateAsync_PutsStateToWebhook(bool on, string expectedState)
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var service = Create(new HttpClient(handler));

        await service.SetPowerStateAsync(on);

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal(WebhookUrl, handler.LastRequest.RequestUri!.ToString());
        Assert.Contains($"\"state\":\"{expectedState}\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task SetPowerStateAsync_WhenWebhookFails_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError);
        var service = Create(new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.SetPowerStateAsync(true));
    }

    // ─── GetStatusAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_WhenWebhookReturnsStatus_ReturnsParsedDto()
    {
        const string json = """{"status":"ok","reachable":true,"powerState":"on","label":"Bereit"}""";
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var service = Create(new HttpClient(handler));

        var dto = await service.GetStatusAsync();

        Assert.True(dto.Reachable);
        Assert.Equal("on", dto.PowerState);
        Assert.Equal("Bereit", dto.Label);
    }

    [Fact]
    public async Task GetStatusAsync_WhenWebhookReturnsError_ReturnsUnreachable()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadGateway);
        var service = Create(new HttpClient(handler));

        var dto = await service.GetStatusAsync();

        Assert.False(dto.Reachable);
        Assert.Equal("Offline", dto.Label);
    }

    [Fact]
    public async Task GetStatusAsync_WhenBodyIsNull_ReturnsUnreachable()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "null");
        var service = Create(new HttpClient(handler));

        var dto = await service.GetStatusAsync();

        Assert.False(dto.Reachable);
    }

    [Fact]
    public async Task GetStatusAsync_OnTimeout_ReturnsUnreachable()
    {
        var handler = new StubHttpMessageHandler(new TaskCanceledException("timeout"));
        var service = Create(new HttpClient(handler));

        var dto = await service.GetStatusAsync();

        Assert.False(dto.Reachable);
        Assert.Contains("Timeout", dto.Message);
    }

    [Fact]
    public async Task GetStatusAsync_OnNetworkError_ReturnsUnreachable()
    {
        var handler = new StubHttpMessageHandler(new HttpRequestException("connection refused"));
        var service = Create(new HttpClient(handler));

        var dto = await service.GetStatusAsync();

        Assert.False(dto.Reachable);
        Assert.Equal("Offline", dto.Label);
    }
}
