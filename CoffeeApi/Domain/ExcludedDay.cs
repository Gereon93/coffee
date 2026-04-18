namespace CoffeeApi.Domain;

/// <summary>
/// Marks a specific day as a mass-import / backfill day so it is
/// excluded from statistics aggregation and anomaly detection.
/// The underlying snapshots stay in the DB unchanged.
/// </summary>
public class ExcludedDay
{
    /// <summary>Local-date representation (yyyy-MM-dd). Primary key.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Free-text reason (e.g. "BSH API outage Feb 2026").</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
