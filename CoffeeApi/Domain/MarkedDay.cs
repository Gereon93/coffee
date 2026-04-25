namespace CoffeeApi.Domain;

/// <summary>
/// A day with a manual annotation. Two kinds:
/// - "mass-import": excluded from stats and anomaly detection
/// - "event":       valid data, but flagged for explanation (birthday, visitors, ...)
/// One MarkedDay per Date (kind decides semantics).
/// </summary>
public class MarkedDay
{
    /// <summary>Local-date representation (yyyy-MM-dd). Primary key.</summary>
    public DateOnly Date { get; set; }

    /// <summary>"mass-import" or "event"</summary>
    public string Kind { get; set; } = "mass-import";

    /// <summary>
    /// Required when Kind="event": "birthday"|"visitors"|"party"|"sick"|"vacation"|"other".
    /// Null for Kind="mass-import".
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>Free-text reason / note. Required for mass-import, optional for event.</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
