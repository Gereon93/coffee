using CoffeeApi.Domain;

namespace CoffeeTest.Domain;

public class MachineSnapshotTests
{
    [Fact]
    public void TotalBeverages_SumsAllCountersExceptHotWaterMl()
    {
        var snapshot = new MachineSnapshot
        {
            BeverageCounterCoffee = 100,
            BeverageCounterCoffeeAndMilk = 10,
            BeverageCounterMilk = 5,
            BeverageCounterHotWaterCups = 3,
            BeverageCounterHotWater = 500 // ml - should NOT be included
        };

        Assert.Equal(118, snapshot.TotalBeverages);
    }

    [Fact]
    public void TotalBeverages_ZeroWhenAllCountersZero()
    {
        var snapshot = new MachineSnapshot();
        Assert.Equal(0, snapshot.TotalBeverages);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var snapshot = new MachineSnapshot();

        Assert.Equal("EQ900-DEFAULT", snapshot.MachineId);
        Assert.Equal("Ready", snapshot.OperationState);
    }
}
