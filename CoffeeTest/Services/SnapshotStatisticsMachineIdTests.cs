using CoffeeTest.Helpers;

namespace CoffeeTest.Services;

public class SnapshotStatisticsMachineIdTests
{
    [Fact]
    public async Task GetDailySummary_ScopesResultsToRequestedMachine()
    {
        await using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        var baseTime = new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc);
        var machineAMorning = new SnapshotBuilder().At(baseTime).WithCoffee(100).WithMachineId("EQ900-A").Build();
        var machineBMorning = new SnapshotBuilder().At(baseTime).WithCoffee(200).WithMachineId("EQ900-B").Build();
        var machineBAfternoon = new SnapshotBuilder().At(baseTime.AddHours(2)).WithCoffee(205).WithMachineId("EQ900-B").Build();

        db.MachineSnapshots.AddRange(machineAMorning, machineBMorning, machineBAfternoon);
        await db.SaveChangesAsync();

        var result = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7), machineId: "EQ900-B");

        Assert.Equal(5, result.CoffeeToday);
        Assert.Equal(0, result.MilkDrinksToday);
        Assert.Equal(5, result.TotalToday);
    }

    [Fact]
    public async Task GetRangeAggregate_ScopesResultsToRequestedMachine()
    {
        await using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        var machineBBaseline = new SnapshotBuilder().At(new DateTime(2026, 2, 5, 23, 0, 0, DateTimeKind.Utc))
            .WithCoffee(10).WithMachineId("EQ900-B").Build();
        var machineBFeb6 = new SnapshotBuilder().At(new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc))
            .WithCoffee(13).WithMachineId("EQ900-B").Build();
        var machineBFeb7 = new SnapshotBuilder().At(new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc))
            .WithCoffee(17).WithMachineId("EQ900-B").Build();
        var machineAFeb6 = new SnapshotBuilder().At(new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc))
            .WithCoffee(200).WithMachineId("EQ900-A").Build();
        var machineAFeb7 = new SnapshotBuilder().At(new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc))
            .WithCoffee(300).WithMachineId("EQ900-A").Build();

        db.MachineSnapshots.AddRange(machineBBaseline, machineBFeb6, machineBFeb7, machineAFeb6, machineAFeb7);
        await db.SaveChangesAsync();

        var result = await service.GetRangeAggregateAsync(
            new DateOnly(2026, 2, 6),
            new DateOnly(2026, 2, 7),
            machineId: "EQ900-B");

        Assert.Equal(2, result.Count);
        Assert.Equal("2026-02-06", result[0].Date);
        Assert.Equal(3, result[0].CoffeeCount);
        Assert.Equal("2026-02-07", result[1].Date);
        Assert.Equal(4, result[1].CoffeeCount);
    }

    [Fact]
    public async Task GetHeatmapData_ScopesResultsToRequestedMachine()
    {
        await using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        var monday = DateTime.UtcNow.Date;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);

        var machineAMondayMorning = new SnapshotBuilder().At(monday.AddHours(10))
            .WithCoffee(100).WithMachineId("EQ900-A").Build();
        var machineAMondayLater = new SnapshotBuilder().At(monday.AddHours(11))
            .WithCoffee(102).WithMachineId("EQ900-A").Build();
        var machineBMondayMorning = new SnapshotBuilder().At(monday.AddHours(10))
            .WithCoffee(10).WithMachineId("EQ900-B").Build();
        var machineBMondayLater = new SnapshotBuilder().At(monday.AddHours(11))
            .WithCoffee(15).WithMachineId("EQ900-B").Build();

        db.MachineSnapshots.AddRange(machineAMondayMorning, machineAMondayLater, machineBMondayMorning, machineBMondayLater);
        await db.SaveChangesAsync();

        var result = await service.GetHeatmapDataAsync(machineId: "EQ900-B");

        Assert.Single(result);
        Assert.Equal(1, result[0].DayOfWeek);
        Assert.Equal(11, result[0].Hour);
        Assert.Equal(5, result[0].Count);
    }
}
