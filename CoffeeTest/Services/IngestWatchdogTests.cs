using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoffeeTest.Services;

public class IngestWatchdogTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTime Noon = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<ISnapshotService, SnapshotService>();
        return services.BuildServiceProvider();
    }

    private static IngestWatchdog CreateWatchdog(
        ServiceProvider provider,
        RecordingLogger<IngestWatchdog> logger,
        DateTime nowUtc)
    {
        var options = Options.Create(new WatchdogOptions
        {
            StaleAfterMinutes = 60,
            QuietFromUtcHour = 1,
            QuietToUtcHour = 6
        });

        return new IngestWatchdog(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            logger,
            new FixedTimeProvider(nowUtc));
    }

    private static async Task SeedSnapshotAsync(ServiceProvider provider, DateTime timestamp)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.MachineSnapshots.Add(new SnapshotBuilder().At(timestamp).WithCoffee(42).Build());
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task StaleIngest_LogsError()
    {
        await using var provider = BuildProvider(Guid.NewGuid().ToString());
        await SeedSnapshotAsync(provider, Noon.AddHours(-3));
        var logger = new RecordingLogger<IngestWatchdog>();
        var watchdog = CreateWatchdog(provider, logger, Noon);

        await watchdog.CheckOnceAsync(CancellationToken.None);

        var errors = logger.MessagesAt(LogLevel.Error);
        Assert.Single(errors);
        Assert.Contains("180", errors[0]);
    }

    [Fact]
    public async Task StaleIngest_LogsErrorOnlyOncePerOutage()
    {
        await using var provider = BuildProvider(Guid.NewGuid().ToString());
        await SeedSnapshotAsync(provider, Noon.AddHours(-3));
        var logger = new RecordingLogger<IngestWatchdog>();
        var watchdog = CreateWatchdog(provider, logger, Noon);

        await watchdog.CheckOnceAsync(CancellationToken.None);
        await watchdog.CheckOnceAsync(CancellationToken.None);

        Assert.Single(logger.MessagesAt(LogLevel.Error));
    }

    [Fact]
    public async Task NoSnapshotEverIngested_AlarmsWithoutNullPlaceholders()
    {
        await using var provider = BuildProvider(Guid.NewGuid().ToString());
        var logger = new RecordingLogger<IngestWatchdog>();
        var watchdog = CreateWatchdog(provider, logger, Noon);

        await watchdog.CheckOnceAsync(CancellationToken.None);

        var error = Assert.Single(logger.MessagesAt(LogLevel.Error));
        Assert.DoesNotContain("(null)", error);
        Assert.Contains("has ever been ingested", error);
    }

    [Fact]
    public async Task FreshIngest_LogsNoError()
    {
        await using var provider = BuildProvider(Guid.NewGuid().ToString());
        await SeedSnapshotAsync(provider, Noon.AddMinutes(-10));
        var logger = new RecordingLogger<IngestWatchdog>();
        var watchdog = CreateWatchdog(provider, logger, Noon);

        await watchdog.CheckOnceAsync(CancellationToken.None);

        Assert.Empty(logger.MessagesAt(LogLevel.Error));
    }

    [Fact]
    public async Task InsideQuietWindow_LogsNoError()
    {
        var night = new DateTime(2026, 8, 15, 3, 0, 0, DateTimeKind.Utc);
        await using var provider = BuildProvider(Guid.NewGuid().ToString());
        await SeedSnapshotAsync(provider, night.AddHours(-3));
        var logger = new RecordingLogger<IngestWatchdog>();
        var watchdog = CreateWatchdog(provider, logger, night);

        await watchdog.CheckOnceAsync(CancellationToken.None);

        Assert.Empty(logger.MessagesAt(LogLevel.Error));
    }

    [Fact]
    public async Task RecoveryAfterAlert_LogsInformation()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var provider = BuildProvider(dbName);
        await SeedSnapshotAsync(provider, Noon.AddHours(-3));
        var logger = new RecordingLogger<IngestWatchdog>();
        var staleWatchdog = CreateWatchdog(provider, logger, Noon);
        await staleWatchdog.CheckOnceAsync(CancellationToken.None);

        await SeedSnapshotAsync(provider, Noon.AddMinutes(-5));
        await staleWatchdog.CheckOnceAsync(CancellationToken.None);

        Assert.Single(logger.MessagesAt(LogLevel.Error));
        Assert.Single(logger.MessagesAt(LogLevel.Information));
    }

    [Fact]
    public async Task FailingSnapshotRead_LogsWarningAndDoesNotAlarm()
    {
        // A broken database is not an ingest outage — it must not fire the same
        // alarm, and it must not take the watchdog loop down either.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ISnapshotService, ThrowingSnapshotService>();
        await using var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger<IngestWatchdog>();
        var watchdog = CreateWatchdog(provider, logger, Noon);

        var exception = await Record.ExceptionAsync(() => watchdog.CheckOnceAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Empty(logger.MessagesAt(LogLevel.Error));
        Assert.Single(logger.MessagesAt(LogLevel.Warning));
    }

    private sealed class ThrowingSnapshotService : ISnapshotService
    {
        public Task<MachineSnapshot?> GetLatestAsync(string machineId = "EQ900-DEFAULT") =>
            throw new InvalidOperationException("database unreachable");

        public Task<(bool Created, MachineSnapshot Snapshot)> ProcessIngestAsync(IngestPayloadDto payload) =>
            throw new NotSupportedException();

        public Task<(List<MachineSnapshot> Items, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 50) =>
            throw new NotSupportedException();

        public Task<List<MachineSnapshot>> GetByDateAsync(DateOnly date, int tzOffsetMinutes = 0) =>
            throw new NotSupportedException();

        public Task<List<MachineSnapshot>> GetByDateRangeAsync(DateOnly from, DateOnly to, int tzOffsetMinutes = 0) =>
            throw new NotSupportedException();

        public Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date, int tzOffsetMinutes = 0) =>
            throw new NotSupportedException();

        public Task<List<HeatmapDataPointDto>> GetHeatmapDataAsync(int weeks = 4, int tzOffsetMinutes = 0) =>
            throw new NotSupportedException();

        public Task<MachineSnapshot?> GetLastSnapshotBeforeAsync(DateTime timestamp) =>
            throw new NotSupportedException();

        public Task<bool> IsDatabaseReachableAsync() => throw new NotSupportedException();
    }
}
