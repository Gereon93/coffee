namespace CoffeeApi.DTOs;

public class HistoricalBackfillRequestDto
{
    public string CommissionedAt { get; set; } = string.Empty;
}

public class HistoricalBackfillPlanDto
{
    public string CommissionedAt { get; set; } = string.Empty;
    public DateOnly EndDate { get; set; }
    public int FirstSnapshotId { get; set; }
    public DateTime FirstSnapshotTimestamp { get; set; }
    public int EstimatedSnapshotCount { get; set; }
    public int TargetBeverageCount { get; set; }
    public int TargetTotalBeverages { get; set; }
    public bool AlreadyApplied { get; set; }
}
