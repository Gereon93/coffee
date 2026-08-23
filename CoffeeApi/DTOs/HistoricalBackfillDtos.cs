namespace CoffeeApi.DTOs;

internal static class HistoricalBackfillDefaults
{
    internal const string MachineId = "EQ900-DEFAULT";
}

public class HistoricalBackfillRequestDto
{
    public string CommissionedAt { get; set; } = string.Empty;

    /// <summary>Machine whose first real snapshot defines the backfill target.</summary>
    public string MachineId { get; set; } = HistoricalBackfillDefaults.MachineId;
}

public class HistoricalBackfillPlanDto
{
    public string CommissionedAt { get; set; } = string.Empty;

    /// <summary>Machine to which the plan and generated snapshots belong.</summary>
    public string MachineId { get; set; } = string.Empty;
    public DateOnly EndDate { get; set; }
    public int FirstSnapshotId { get; set; }
    public DateTime FirstSnapshotTimestamp { get; set; }
    public int EstimatedSnapshotCount { get; set; }
    public int TargetBeverageCount { get; set; }
    public int TargetTotalBeverages { get; set; }
    public bool AlreadyApplied { get; set; }
}
