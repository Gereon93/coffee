using CoffeeApi.Services;

namespace CoffeeTest.Services;

public class IngestWatchdogEvaluatorTests
{
    private static WatchdogOptions Options() => new()
    {
        StaleAfterMinutes = 60,
        QuietFromUtcHour = 1,
        QuietToUtcHour = 6
    };

    private static DateTime Utc(int hour, int minute = 0) =>
        new(2026, 8, 15, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void NoSnapshotEver_IsStale()
    {
        var state = IngestWatchdogEvaluator.Evaluate(null, Utc(12), Options());

        Assert.Equal(IngestState.Stale, state);
    }

    [Fact]
    public void SnapshotWithinThreshold_IsOk()
    {
        var state = IngestWatchdogEvaluator.Evaluate(Utc(11, 30), Utc(12), Options());

        Assert.Equal(IngestState.Ok, state);
    }

    [Fact]
    public void SnapshotExactlyAtThreshold_IsStillOk()
    {
        var state = IngestWatchdogEvaluator.Evaluate(Utc(11), Utc(12), Options());

        Assert.Equal(IngestState.Ok, state);
    }

    [Fact]
    public void SnapshotOlderThanThreshold_IsStale()
    {
        var state = IngestWatchdogEvaluator.Evaluate(Utc(10, 59), Utc(12), Options());

        Assert.Equal(IngestState.Stale, state);
    }

    [Fact]
    public void SnapshotInLocalTime_IsTreatedAsUtc()
    {
        // Snapshots are stored as UTC; a Kind of Unspecified must not shift the age.
        var lastSnapshot = new DateTime(2026, 8, 15, 11, 30, 0, DateTimeKind.Unspecified);

        var state = IngestWatchdogEvaluator.Evaluate(lastSnapshot, Utc(12), Options());

        Assert.Equal(IngestState.Ok, state);
    }

    [Fact]
    public void InsideQuietWindow_IsQuiet()
    {
        // 03:00 UTC — n8n's cron does not run, so staleness means nothing here.
        var state = IngestWatchdogEvaluator.Evaluate(Utc(0), Utc(3), Options());

        Assert.Equal(IngestState.Quiet, state);
    }

    [Fact]
    public void AtQuietWindowStart_IsQuiet()
    {
        var state = IngestWatchdogEvaluator.Evaluate(Utc(0), Utc(1), Options());

        Assert.Equal(IngestState.Quiet, state);
    }

    [Fact]
    public void AtQuietWindowEnd_IsEvaluatedNormally()
    {
        // 06:00 UTC is the first minute the window no longer suppresses.
        var state = IngestWatchdogEvaluator.Evaluate(Utc(0), Utc(6), Options());

        Assert.Equal(IngestState.Stale, state);
    }

    [Fact]
    public void QuietWindowWrappingMidnight_IsQuiet()
    {
        var options = new WatchdogOptions
        {
            StaleAfterMinutes = 60,
            QuietFromUtcHour = 23,
            QuietToUtcHour = 2
        };

        var state = IngestWatchdogEvaluator.Evaluate(Utc(0), Utc(0, 30), options);

        Assert.Equal(IngestState.Quiet, state);
    }

    [Fact]
    public void QuietWindowWrappingMidnight_OutsideWindow_IsEvaluatedNormally()
    {
        var options = new WatchdogOptions
        {
            StaleAfterMinutes = 60,
            QuietFromUtcHour = 23,
            QuietToUtcHour = 2
        };

        var state = IngestWatchdogEvaluator.Evaluate(Utc(1), Utc(12), options);

        Assert.Equal(IngestState.Stale, state);
    }

    [Fact]
    public void EqualQuietHours_DisableSuppression()
    {
        var options = new WatchdogOptions
        {
            StaleAfterMinutes = 60,
            QuietFromUtcHour = 0,
            QuietToUtcHour = 0
        };

        var state = IngestWatchdogEvaluator.Evaluate(Utc(0), Utc(3), options);

        Assert.Equal(IngestState.Stale, state);
    }
}
