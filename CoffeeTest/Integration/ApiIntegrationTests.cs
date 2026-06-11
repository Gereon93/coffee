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
}
