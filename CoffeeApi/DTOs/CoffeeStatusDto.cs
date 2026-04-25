namespace CoffeeApi.DTOs;

/// <summary>
/// Live status of the coffee machine, mirrors the schema in BRIEFING_COFFEE_API.md.
/// Returned by GET /coffee/status.
/// </summary>
public class CoffeeStatusDto
{
    /// <summary>"ok" | "error". Always "ok" when the API itself is healthy (use reachable=false for BSH outages).</summary>
    public string Status { get; set; } = "ok";

    /// <summary>True if the n8n/HomeConnect chain delivered fresh data.</summary>
    public bool Reachable { get; set; }

    /// <summary>"on" | "off" | "standby". Null when reachable=false.</summary>
    public string? PowerState { get; set; }

    /// <summary>"inactive" | "ready" | "run" | "pause" | "finished" | "error". Null when reachable=false.</summary>
    public string? OperationState { get; set; }

    /// <summary>Pre-formatted German label, e.g. "Bereit", "Aus", "Heizt auf", "Offline".</summary>
    public string Label { get; set; } = "Unbekannt";

    /// <summary>UTC timestamp of the last successful HomeConnect read.</summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>Optional message — populated when reachable=false or for diagnostic info.</summary>
    public string? Message { get; set; }
}
