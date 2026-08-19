namespace CoffeeApi.Domain;

/// <summary>
/// The single definition of the local-day rule. Snapshots are stored in UTC,
/// but every statistic is reported for the caller's local day, derived from a
/// fixed UTC offset in minutes (see ADR-004).
/// </summary>
public static class LocalDay
{
    /// <summary>
    /// Calculates the UTC start (inclusive) and end (exclusive) of a local day.
    /// </summary>
    public static (DateTime Start, DateTime End) BoundsUtc(DateOnly date, int tzOffsetMinutes)
    {
        // Local midnight in UTC = midnight - offset
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            .AddMinutes(-tzOffsetMinutes);
        var end = start.AddDays(1);
        return (start, end);
    }

    /// <summary>
    /// Converts a stored UTC timestamp into the caller's local wall-clock time.
    /// </summary>
    public static DateTime ToLocal(DateTime timestampUtc, int tzOffsetMinutes)
    {
        return timestampUtc.AddMinutes(tzOffsetMinutes);
    }

    /// <summary>
    /// The local calendar date a stored UTC timestamp falls on.
    /// </summary>
    public static DateOnly DateOf(DateTime timestampUtc, int tzOffsetMinutes)
    {
        return DateOnly.FromDateTime(ToLocal(timestampUtc, tzOffsetMinutes));
    }
}
