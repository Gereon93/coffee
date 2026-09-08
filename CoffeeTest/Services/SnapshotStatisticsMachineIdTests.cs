using CoffeeTest.Helpers;

namespace CoffeeTest.Services;

public class SnapshotStatisticsMachineIdTests
{
    [Fact]
    public async Task GetDailySummary_ScopesResultsToRequestedMachine()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        var baseTime = new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc);
        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(baseTime).WithCoffee(100).WithMachineId("EQ900-A").Build(),
            new SnapshotBuilder().At(baseTime).WithCoffee(200).WithMachineId("EQ900-B").Build(),
            new SnapshotBuilder().At(baseTime.AddHours(2)).WithCoffee(205).WithMachineId("EQ900-B").Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7), machineId: "EQ900-B");

        Assert.Equal(5, result.CoffeeToday);
        Assert.Equal(0, result.MilkDrinksToday);
        Assert.Equal(5, result.TotalToday);
    }

    [Fact]
    public async Task GetRangeAggregate_ScopesResultsToRequestedMachine()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        db.MachineSnapshots.AddRange(
            // Baseline for machine B before the requested range
            new SnapshotBuilder().At(new DateTime(2026, 2, 5, 23, 0, 0, DateTimeKind.Utc))
                .WithCoffee(10).WithMachineId("EQ900-B").Build(),
            // Machine B snapshots inside the range
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc))
                .WithCoffee(13).WithMachineId("EQ900-B").Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc))
                .WithCoffee(17).WithMachineId("EQ900-B").Build(),
            // Machine A snapshots with the same timestamps but different deltas
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc))
                .WithCoffee(200).WithMachineId("EQ900-A").Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc))
                .WithCoffee(300).WithMachineId("EQ900-A").Build()
        );
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
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        var monday = DateTime.UtcNow.Date;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);

        db.MachineSnapshots.AddRange(
            // Machine A: Monday 10:00 -> 11:00, +2 coffees
            new SnapshotBuilder().At(monday.AddHours(10))
                .WithCoffee(100).WithMachineId("EQ900-A").Build(),
            new SnapshotBuilder().At(monday.AddHours(11))
                .WithCoffee(102).WithMachineId("EQ900-A").Build(),
            // Machine B: Monday 10:00 -> 11:00, +5 coffees
            new SnapshotBuilder().At(monday.AddHours(10))
                .WithCoffee(10).WithMachineId("EQ900-B").Build(),
            new SnapshotBuilder().At(monday.AddHours(11))
                .WithCoffee(15).WithMachineId("EQ900-B").Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetHeatmapDataAsync(4, machineId: "EQ900-B");

        Assert.Single(result);
        Assert.Equal(1, result[0].DayOfWeek);
        Assert.Equal(11, result[0].Hour);
        Assert.Equal(5, result[0].Count);
    }
}
