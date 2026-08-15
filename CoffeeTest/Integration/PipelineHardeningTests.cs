using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CoffeeApi.Infrastructure;

namespace CoffeeTest.Integration;

/// <summary>
/// Covers the request-pipeline decisions that only show up when the real host
/// boots: the API documentation is Development-only, the power relay is rate
/// limited, and the proxy's forwarded client address reaches the logs.
/// </summary>
public class PipelineHardeningTests
{
    private const string ApiKey = "pipeline-test-key";

    /// <summary>
    /// Boots the real <see cref="CoffeeApi.Program"/> against a throwaway SQLite
    /// file in the given environment.
    /// </summary>
    private sealed class Factory : WebApplicationFactory<CoffeeApi.Program>
    {
        private readonly string _dbPath =
            Path.Combine(Path.GetTempPath(), $"coffee-pipeline-{Guid.NewGuid():N}.db");

        private readonly string _environment;
        private readonly Dictionary<string, string> _settings;

        public Factory(string environment, Dictionary<string, string>? settings = null)
        {
            _environment = environment;
            _settings = settings ?? [];
        }

        /// <summary>Warnings and errors logged by the booted app.</summary>
        public List<string> LogMessages { get; } = [];

        /// <summary>
        /// TestServer leaves <c>Connection.RemoteIpAddress</c> unset, so the
        /// forwarded-headers middleware would never recognise a known proxy.
        /// </summary>
        public IPAddress? RemoteIpAddress { get; set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.UseSetting("ApiKey", ApiKey);
            foreach (var (key, value) in _settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureLogging(logging =>
                logging.AddProvider(new CollectingLoggerProvider(LogMessages)));

            builder.ConfigureTestServices(services =>
            {
                var remoteIp = RemoteIpAddress;
                if (remoteIp is not null)
                {
                    services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(remoteIp));
                }
            });
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

    /// <summary>Runs ahead of the application pipeline and stamps the peer address.</summary>
    private sealed class RemoteIpStartupFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = address;
                    await nextMiddleware();
                });
                next(app);
            };
    }

    private sealed class CollectingLoggerProvider(List<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(sink);

        public void Dispose() { }

        private sealed class CollectingLogger(List<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                lock (sink)
                {
                    sink.Add(formatter(state, exception));
                }
            }
        }
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    public async Task ApiDocumentation_InProduction_IsNotServed(string path)
    {
        using var factory = new Factory("Production");
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApiDocumentation_InDevelopment_IsServed()
    {
        using var factory = new Factory("Development");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Power_BeyondTheWindowPermitLimit_Returns429()
    {
        using var factory = new Factory("Development");
        var client = factory.CreateClient();
        var statusCodes = new List<HttpStatusCode>();

        // The limiter sits in front of the API-key check, so unauthenticated
        // calls consume permits too — that is the point of the guard.
        for (var i = 0; i < 11; i++)
        {
            var response = await client.PostAsync(
                "/coffee/power",
                new StringContent("""{"state":"on"}""", Encoding.UTF8, "application/json"));
            statusCodes.Add(response.StatusCode);
        }

        Assert.All(statusCodes.Take(10), code => Assert.Equal(HttpStatusCode.Unauthorized, code));
        Assert.Equal(HttpStatusCode.TooManyRequests, statusCodes[10]);
    }

    [Fact]
    public async Task RejectedRequest_BehindConfiguredProxy_LogsTheForwardedCallerIp()
    {
        using var factory = new Factory(
            "Development",
            new Dictionary<string, string> { ["ForwardedHeaders:KnownNetworks:0"] = "127.0.0.0/8" })
        {
            RemoteIpAddress = IPAddress.Loopback
        };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");

        var response = await client.PostAsync(
            "/api/ingest",
            new StringContent("""{"data":{"status":[]}}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(factory.LogMessages, m => m.Contains("203.0.113.7", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectedRequest_WithoutConfiguredProxy_LogsThePeerAddress()
    {
        using var factory = new Factory("Development") { RemoteIpAddress = IPAddress.Loopback };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");

        var response = await client.PostAsync(
            "/api/ingest",
            new StringContent("""{"data":{"status":[]}}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(factory.LogMessages, m => m.Contains("203.0.113.7", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Migrations_LeaveNoUnusedIdempotencyIndex()
    {
        using var factory = new Factory("Development");
        _ = factory.CreateClient(); // boots the host, which runs the migrations

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='MachineSnapshots'";
        var indexes = new List<string>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }
        }

        Assert.Contains("IX_MachineSnapshots_Timestamp", indexes);
        Assert.DoesNotContain("IX_MachineSnapshots_Idempotency", indexes);
    }
}
