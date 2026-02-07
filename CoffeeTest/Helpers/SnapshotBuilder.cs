using CoffeeApi.Domain;

namespace CoffeeTest.Helpers;

/// <summary>
/// Fluent builder for creating test MachineSnapshot instances
/// </summary>
public class SnapshotBuilder
{
    private readonly MachineSnapshot _snapshot = new();

    public SnapshotBuilder At(DateTime timestamp)
    {
        _snapshot.Timestamp = timestamp;
        _snapshot.CreatedAt = timestamp;
        return this;
    }

    public SnapshotBuilder WithCoffee(int count)
    {
        _snapshot.BeverageCounterCoffee = count;
        return this;
    }

    public SnapshotBuilder WithMilk(int count)
    {
        _snapshot.BeverageCounterMilk = count;
        return this;
    }

    public SnapshotBuilder WithCoffeeAndMilk(int count)
    {
        _snapshot.BeverageCounterCoffeeAndMilk = count;
        return this;
    }

    public SnapshotBuilder WithHotWaterCups(int count)
    {
        _snapshot.BeverageCounterHotWaterCups = count;
        return this;
    }

    public SnapshotBuilder WithState(string state)
    {
        _snapshot.OperationState = state;
        return this;
    }

    public MachineSnapshot Build() => _snapshot;
}
