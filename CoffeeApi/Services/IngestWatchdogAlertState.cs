namespace CoffeeApi.Services;

/// <summary>What the watchdog should report after an evaluation.</summary>
public enum WatchdogAction
{
    None,
    Alert,
    Recovered
}

/// <summary>
/// Turns a stream of <see cref="IngestState"/> readings into edge-triggered
/// actions: one alarm per outage, one recovery notice per repair. The quiet
/// window leaves an active alarm untouched so the same outage is not reported
/// twice once the window ends.
/// </summary>
public sealed class IngestWatchdogAlertState
{
    private bool _alerted;

    public WatchdogAction Advance(IngestState state)
    {
        switch (state)
        {
            case IngestState.Stale when !_alerted:
                _alerted = true;
                return WatchdogAction.Alert;

            case IngestState.Ok when _alerted:
                _alerted = false;
                return WatchdogAction.Recovered;

            default:
                return WatchdogAction.None;
        }
    }
}
