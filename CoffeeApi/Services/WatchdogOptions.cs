namespace CoffeeApi.Services;

/// <summary>
/// Configuration for the ingest watchdog, bound from the <c>Watchdog</c> section.
/// </summary>
public sealed class WatchdogOptions
{
    /// <summary>Turns the watchdog off entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the watchdog compares the latest snapshot against the clock.</summary>
    public int CheckIntervalMinutes { get; set; } = 5;

    /// <summary>Smallest interval a timer can be built from.</summary>
    public const int MinimumCheckIntervalMinutes = 1;

    /// <summary>
    /// <see cref="CheckIntervalMinutes"/> clamped to something a timer accepts.
    /// A non-positive period makes <c>PeriodicTimer</c> throw, and an exception
    /// out of a <c>BackgroundService</c> stops the host by default — a mistyped
    /// interval must not take down the API the watchdog is there to watch.
    /// </summary>
    public TimeSpan EffectiveCheckInterval =>
        TimeSpan.FromMinutes(Math.Max(MinimumCheckIntervalMinutes, CheckIntervalMinutes));

    /// <summary>
    /// Age of the newest snapshot that still counts as healthy. n8n ingests every
    /// 15 minutes, so 60 minutes means four missed runs before the alarm fires.
    /// </summary>
    public int StaleAfterMinutes { get; set; } = 60;

    /// <summary>
    /// Start of the daily window in which no ingest is scheduled (UTC hour, inclusive).
    /// </summary>
    public int QuietFromUtcHour { get; set; } = 1;

    /// <summary>
    /// End of the daily window in which no ingest is scheduled (UTC hour, exclusive).
    /// Equal to <see cref="QuietFromUtcHour"/> disables suppression.
    /// </summary>
    public int QuietToUtcHour { get; set; } = 6;
}
