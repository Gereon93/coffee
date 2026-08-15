using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoffeeTest.Integration;

/// <summary>
/// End-to-end tests that boot the real ASP.NET Core pipeline via
/// <see cref="WebApplicationFactory{TEntryPoint}"/> — routing, middleware,
/// EF Core migrations and SQLite all run for real against an isolated,
/// throwaway database file.
/// </summary>
public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.CoffeeApiFactory>
{
    private const string ApiKey = "integration-test-key";

    public sealed class CoffeeApiFactory : WebApplicationFactory<CoffeeApi.Program>
    {
        private readonly string _dbPath =
            Path.Combine(Path.GetTempPath(), $"coffee-it-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.UseSetting("ApiKey", ApiKey);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }

    private readonly CoffeeApiFactory _factory;

    public ApiIntegrationTests(CoffeeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_OnFreshDatabase_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/ingest",
            new StringContent("""{"data":{"status":[]}}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithValidApiKey_PersistsSnapshotAndIsReadableViaStats()
    {
        var client = _factory.CreateClient();
        const string payload =
            """{"data":{"status":[{"key":"ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee","value":42}]}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-API-Key", ApiKey);

        var ingestResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, ingestResponse.StatusCode);

        // The persisted snapshot must be visible through the read API.
        var statsResponse = await client.GetAsync("/api/stats");
        statsResponse.EnsureSuccessStatusCode();
        var body = await statsResponse.Content.ReadAsStringAsync();
        Assert.Contains("42", body);
    }

    [Fact]
    public async Task Power_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/coffee/power",
            new StringContent("""{"state":"on"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Power_WithValidApiKey_PassesAuthentication()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/coffee/power")
        {
            Content = new StringContent("""{"state":"nonsense"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-API-Key", ApiKey);

        var response = await client.SendAsync(request);

        // An invalid state reaches the action and is rejected there, which proves
        // the request got past the API key middleware. Asserting on 400 rather
        // than 200 keeps the test from actuating the n8n webhook.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoffeeStatus_WithoutApiKey_IsNotBlocked()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/coffee/status");

        // Reads stay open — only /coffee/power is protected.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMarkedDay_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/stats/marked-days",
            new StringContent(
                """{"date":"2026-01-05","kind":"mass-import","reason":"holiday"}""",
                Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMarkedDay_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/stats/marked-days/2026-01-05");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMarkedDays_WithoutApiKey_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/stats/marked-days");

        // Same path as the protected writes — the read must stay open.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateMarkedDay_WithValidApiKey_IsCreatedAndDeletable()
    {
        var client = _factory.CreateClient();
        const string date = "2026-01-06";

        var create = new HttpRequestMessage(HttpMethod.Post, "/api/stats/marked-days")
        {
            Content = new StringContent(
                $$"""{"date":"{{date}}","kind":"mass-import","reason":"integration test"}""",
                Encoding.UTF8,
                "application/json"),
        };
        create.Headers.Add("X-API-Key", ApiKey);

        var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/stats/marked-days/{date}");
        delete.Headers.Add("X-API-Key", ApiKey);

        var deleteResponse = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
