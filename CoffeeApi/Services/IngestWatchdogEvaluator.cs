namespace CoffeeApi.Services;

/// <summary>Health of the n8n ingest as judged from the newest stored snapshot.</summary>
public enum IngestState
{
    /// <summary>A snapshot arrived within the configured threshold.</summary>
    Ok,

    /// <summary>No snapshot within the threshold — the ingest has stopped.</summary>
    Stale,

    /// <summary>No ingest is scheduled right now, so staleness carries no information.</summary>
    Quiet
}

/// <summary>
/// Pure staleness rule of the ingest watchdog, kept separate from the hosted
/// service so it can be tested without a clock or a database.
/// </summary>
public static class IngestWatchdogEvaluator
{
    public static IngestState Evaluate(DateTime? lastSnapshotUtc, DateTime nowUtc, WatchdogOptions options)
    {
        if (IsQuietHour(nowUtc.Hour, options))
        {
            return IngestState.Quiet;
        }

        if (lastSnapshotUtc is null)
        {
            return IngestState.Stale;
        }

        var age = nowUtc - DateTime.SpecifyKind(lastSnapshotUtc.Value, DateTimeKind.Utc);
        return age.TotalMinutes > options.StaleAfterMinutes ? IngestState.Stale : IngestState.Ok;
    }

    private static bool IsQuietHour(int hourUtc, WatchdogOptions options)
    {
        var from = options.QuietFromUtcHour;
        var to = options.QuietToUtcHour;

        if (from == to)
        {
            return false;
        }

        return from < to
            ? hourUtc >= from && hourUtc < to
            : hourUtc >= from || hourUtc < to;
    }
}
