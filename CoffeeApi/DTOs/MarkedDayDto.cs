namespace CoffeeApi.DTOs;

public class MarkedDayDto
{
    /// <summary>Local date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>"mass-import" or "event"</summary>
    public string Kind { get; set; } = "mass-import";

    /// <summary>
    /// Required when Kind="event": birthday|visitors|party|sick|vacation|other.
    /// Null for Kind="mass-import".
    /// </summary>
    public string? EventType { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class CreateMarkedDayDto
{
    /// <summary>Local date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>"mass-import" or "event". Defaults to "mass-import" if omitted (backward-compat).</summary>
    public string Kind { get; set; } = "mass-import";

    /// <summary>Required when Kind="event"; ignored otherwise.</summary>
    public string? EventType { get; set; }

    public string Reason { get; set; } = string.Empty;
}
