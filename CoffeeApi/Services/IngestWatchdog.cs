using Microsoft.Extensions.Options;

namespace CoffeeApi.Services;

/// <summary>
/// Watches the age of the newest snapshot and reports when the n8n ingest has
/// stopped. The alarm is an <see cref="LogLevel.Error"/> log entry: Sentry's
/// ASP.NET Core integration promotes those to GlitchTip events, so no separate
/// alerting channel is needed.
/// </summary>
public sealed partial class IngestWatchdog : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WatchdogOptions _options;
    private readonly ILogger<IngestWatchdog> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IngestWatchdogAlertState _alertState = new();

    public IngestWatchdog(
        IServiceScopeFactory scopeFactory,
        IOptions<WatchdogOptions> options,
        ILogger<IngestWatchdog> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Ingest watchdog is disabled");
            return;
        }

        LogWatchdogStarted(
            _options.StaleAfterMinutes,
            _options.QuietFromUtcHour,
            _options.QuietToUtcHour);

        var interval = _options.EffectiveCheckInterval;
        if (interval != TimeSpan.FromMinutes(_options.CheckIntervalMinutes))
        {
            _logger.LogWarning(
                "Watchdog:CheckIntervalMinutes is {ConfiguredInterval}; using {EffectiveInterval} instead",
                _options.CheckIntervalMinutes,
                interval);
        }

        using var timer = new PeriodicTimer(interval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckOnceAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Runs a single staleness check and reports the resulting edge, if any.
    /// </summary>
    public async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        DateTime? lastSnapshotUtc;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var snapshots = scope.ServiceProvider.GetRequiredService<ISnapshotQueryService>();
            lastSnapshotUtc = (await snapshots.GetLatestAsync())?.Timestamp;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failing probe is a database problem, not an ingest outage — it must
            // not raise the same alarm. Database health is reported by /api/health.
            _logger.LogWarning(ex, "Ingest watchdog could not read the latest snapshot");
            return;
        }

        var state = IngestWatchdogEvaluator.Evaluate(lastSnapshotUtc, nowUtc, _options);
        var ageMinutes = lastSnapshotUtc is null
            ? (int?)null
            : (int)(nowUtc - DateTime.SpecifyKind(lastSnapshotUtc.Value, DateTimeKind.Utc)).TotalMinutes;

        switch (_alertState.Advance(state))
        {
            case WatchdogAction.Alert when ageMinutes is null:
                _logger.LogError(
                    "n8n ingest stalled: no snapshot has ever been ingested (threshold {StaleAfterMinutes} minutes)",
                    _options.StaleAfterMinutes);
                break;

            case WatchdogAction.Alert:
                _logger.LogError(
                    "n8n ingest stalled: no snapshot for {AgeMinutes} minutes (threshold {StaleAfterMinutes} minutes, last snapshot {LastSnapshotUtc})",
                    ageMinutes,
                    _options.StaleAfterMinutes,
                    lastSnapshotUtc);
                break;

            case WatchdogAction.Recovered:
                LogIngestRecovered(ageMinutes);
                break;

            case WatchdogAction.None:
            default:
                break;
        }
    }

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Ingest watchdog started: alarm after {StaleAfterMinutes} min without a snapshot, quiet between {QuietFrom}:00 and {QuietTo}:00 UTC")]
    private partial void LogWatchdogStarted(int staleAfterMinutes, int quietFrom, int quietTo);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "n8n ingest recovered: snapshot is {AgeMinutes} minutes old")]
    private partial void LogIngestRecovered(int? ageMinutes);
}
