namespace CoffeeApi.DTOs;

internal static class HistoricalBackfillDefaults
{
    internal const string MachineId = "EQ900-DEFAULT";
}

public class HistoricalBackfillRequestDto
{
    public string CommissionedAt { get; init; } = string.Empty;

    /// <summary>Machine whose first real snapshot defines the backfill target.</summary>
    public string MachineId { get; init; } = HistoricalBackfillDefaults.MachineId;
}

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
