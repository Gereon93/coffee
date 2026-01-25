namespace CoffeeApi.Domain;

/// <summary>
/// Represents a snapshot of the EQ900 coffee machine state at a specific point in time.
/// </summary>
public class MachineSnapshot
{
    public int Id { get; set; }

    /// <summary>
    /// Timestamp of the snapshot (from n8n/Home Connect)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Machine identifier (for future multi-machine support)
    /// </summary>
    public string MachineId { get; set; } = "EQ900-DEFAULT";

    // Beverage Counters
    public int BeverageCounterCoffee { get; set; }
    public int BeverageCounterCoffeeAndMilk { get; set; }
    public int BeverageCounterMilk { get; set; }
    public int BeverageCounterHotWaterCups { get; set; }
    public int BeverageCounterHotWater { get; set; } // in ml

    // Status Fields
    public string OperationState { get; set; } = "Ready";
    public bool RemoteControlAllowed { get; set; }
    public bool LocalControlActive { get; set; }
    public bool InteriorIlluminationActive { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Computed total of all beverage counters (excluding hot water ml)
    /// </summary>
    public int TotalBeverages =>
        BeverageCounterCoffee +
        BeverageCounterCoffeeAndMilk +
        BeverageCounterMilk +
        BeverageCounterHotWaterCups;
}
