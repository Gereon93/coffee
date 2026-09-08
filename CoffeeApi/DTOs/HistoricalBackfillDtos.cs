namespace CoffeeApi.DTOs;

internal static class HistoricalBackfillDefaults
{
    internal const string MachineId = "EQ900-DEFAULT";
}

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// Request body: the setters are used by the JSON deserializer of the model
// binder, not by code in this solution. Get-only would silently bind nothing.
public class HistoricalBackfillRequestDto
{
    public string CommissionedAt { get; init; } = string.Empty;

    /// <summary>Machine whose first real snapshot defines the backfill target.</summary>
    public string MachineId { get; init; } = HistoricalBackfillDefaults.MachineId;
}
// ReSharper restore AutoPropertyCanBeMadeGetOnly.Global

public class HistoricalBackfillPlanDto
{
    public string CommissionedAt { get; init; } = string.Empty;

    /// <summary>Machine to which the plan and generated snapshots belong.</summary>
    public string MachineId { get; init; } = string.Empty;
    public DateOnly EndDate { get; init; }
    public int FirstSnapshotId { get; init; }
    public DateTime FirstSnapshotTimestamp { get; init; }
    public int EstimatedSnapshotCount { get; init; }
    public int TargetBeverageCount { get; init; }
    public int TargetTotalBeverages { get; init; }
    public bool AlreadyApplied { get; init; }
}
