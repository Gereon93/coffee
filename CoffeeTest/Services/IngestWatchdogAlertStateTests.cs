using CoffeeApi.Services;

namespace CoffeeTest.Services;

public class IngestWatchdogAlertStateTests
{
    [Fact]
    public void FirstStale_Alerts()
    {
        var state = new IngestWatchdogAlertState();

        Assert.Equal(WatchdogAction.Alert, state.Advance(IngestState.Stale));
    }

    [Fact]
    public void StaleTwice_AlertsOnlyOnce()
    {
        var state = new IngestWatchdogAlertState();
        state.Advance(IngestState.Stale);

        Assert.Equal(WatchdogAction.None, state.Advance(IngestState.Stale));
    }

    [Fact]
    public void OkWithoutPriorAlert_DoesNothing()
    {
        var state = new IngestWatchdogAlertState();

        Assert.Equal(WatchdogAction.None, state.Advance(IngestState.Ok));
    }

    [Fact]
    public void OkAfterAlert_Recovers()
    {
        var state = new IngestWatchdogAlertState();
        state.Advance(IngestState.Stale);

        Assert.Equal(WatchdogAction.Recovered, state.Advance(IngestState.Ok));
    }

    [Fact]
    public void RecoveryIsReportedOnlyOnce()
    {
        var state = new IngestWatchdogAlertState();
        state.Advance(IngestState.Stale);
        state.Advance(IngestState.Ok);

        Assert.Equal(WatchdogAction.None, state.Advance(IngestState.Ok));
    }

    [Fact]
    public void QuietWindow_DoesNotClearAnActiveAlert()
    {
        // Otherwise the quiet window would re-arm the alarm and it would fire
        // a second time for the same outage once the window ends.
        var state = new IngestWatchdogAlertState();
        state.Advance(IngestState.Stale);
        state.Advance(IngestState.Quiet);

        Assert.Equal(WatchdogAction.None, state.Advance(IngestState.Stale));
    }

    [Fact]
    public void StaleAgainAfterRecovery_AlertsAgain()
    {
        var state = new IngestWatchdogAlertState();
        state.Advance(IngestState.Stale);
        state.Advance(IngestState.Ok);

        Assert.Equal(WatchdogAction.Alert, state.Advance(IngestState.Stale));
    }
}
