using System.Globalization;
using System.Net;
using CoffeeApi.Infrastructure;
using CoffeeApi.Middleware;
using CoffeeApi.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace CoffeeApi
{
    public class Program
    {
        private static readonly string[] DefaultDevOrigins =
            ["http://localhost:5173", "http://localhost:8090"];

        /// <summary>Rate-limiter policy guarding the relay to n8n and BSH.</summary>
        public const string PowerRateLimitPolicy = "coffee-power";
        private const int PowerPermitLimit = 10;
        private static readonly TimeSpan PowerRateLimitWindow = TimeSpan.FromMinutes(1);

        protected Program() { }

        private static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var sentryDsn = Environment.GetEnvironmentVariable("SENTRY_DSN");
            if (!string.IsNullOrWhiteSpace(sentryDsn))
            {
                builder.WebHost.UseSentry(options =>
                {
                    options.Dsn = sentryDsn;
                    options.Environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT")
                        ?? builder.Environment.EnvironmentName;
                    options.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE") ?? "dev";
                    options.SendDefaultPii = false;
                    options.AttachStacktrace = true;
                    options.TracesSampleRate = double.TryParse(
                        Environment.GetEnvironmentVariable("SENTRY_TRACES_SAMPLE_RATE"),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var rate) ? rate : 0.0;
                    options.DefaultTags["service"] = "coffee-api";
                });
            }

            builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: false);

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApi();

            // ===== EQ900 Services (SQLite) =====
            var connectionString = builder.Configuration.GetConnectionString("Default")
                ?? "Data Source=coffee.db";

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connectionString));

            builder.Services.AddScoped<ISnapshotQueryService, SnapshotQueryService>();
            builder.Services.AddScoped<ISnapshotIngestService, SnapshotIngestService>();
            builder.Services.AddScoped<ISnapshotStatisticsService, SnapshotStatisticsService>();
            builder.Services.AddScoped<IMarkedDayService, MarkedDayService>();
            builder.Services.AddScoped<IBeanHopperService, BeanHopperService>();

            // ===== Ingest Watchdog (alarms via Sentry/GlitchTip when n8n stops) =====
            builder.Services.Configure<WatchdogOptions>(builder.Configuration.GetSection("Watchdog"));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddHostedService<IngestWatchdog>();

            // ===== n8n Webhook Service (HomeConnect) =====
            builder.Services.AddHttpClient<IHomeConnectService, HomeConnectService>();
            builder.Services.AddMemoryCache();

            var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            string[] fallbackOrigins = builder.Environment.IsDevelopment() ? DefaultDevOrigins : [];
            var allowedOrigins = configuredOrigins is { Length: > 0 } ? configuredOrigins : fallbackOrigins;

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // /coffee/power relays to n8n and from there to BSH — a fixed window
            // caps how often the machine can be actuated, whoever asks.
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter(PowerRateLimitPolicy, limiter =>
                {
                    limiter.PermitLimit = PowerPermitLimit;
                    limiter.Window = PowerRateLimitWindow;
                    limiter.QueueLimit = 0;
                });
            });

            var app = builder.Build();

            // Baseline pre-migration DBs, then apply pending migrations
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MigrationBaseliner");
                MigrationBaseliner.EnsureBaselined(db, logger);
                db.Database.Migrate();
            }

            // Configure the HTTP request pipeline
            ConfigureForwardedHeaders(app);

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                // Scalar API documentation (replaces Swagger). Development only —
                // in production the spec is readable by anyone who reaches the
                // reverse proxy, and nothing there needs it.
                app.MapOpenApi();
                app.MapScalarApiReference(options =>
                {
                    options.Title = "Coffee Analytics Hub API";
                    options.Theme = ScalarTheme.BluePlanet;
                });
            }

            app.UseCors();
            app.UseRateLimiter();

            // API Key Authentication for protected endpoints
            app.UseApiKeyAuthentication();

            // No UseHttpsRedirection: the container listens on plain HTTP
            // (ASPNETCORE_URLS=http://+:8080); TLS terminates upstream.
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }

        /// <summary>
        /// Honour <c>X-Forwarded-For</c> from the reverse proxy so the client IP in
        /// the logs is the caller's, not nginx's. Only enabled when the proxy
        /// networks are configured — an unrestricted forwarder would let a direct
        /// caller write any address it likes into the log.
        /// </summary>
        private static void ConfigureForwardedHeaders(WebApplication app)
        {
            var networks = app.Configuration
                .GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];

            if (networks.Length == 0)
            {
                return;
            }

            var options = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor
            };
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var entry in networks)
            {
                if (System.Net.IPNetwork.TryParse(entry, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                }
                else if (IPAddress.TryParse(entry, out var proxy))
                {
                    options.KnownProxies.Add(proxy);
                }
                else
                {
                    app.Logger.LogWarning(
                        "ForwardedHeaders:KnownNetworks entry {Entry} is neither a CIDR nor an IP — ignored",
                        entry);
                }
            }

            app.UseForwardedHeaders(options);
        }
    }
}
