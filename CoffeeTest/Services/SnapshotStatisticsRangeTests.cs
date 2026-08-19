using CoffeeTest.Helpers;

namespace CoffeeTest.Services;

public class SnapshotStatisticsRangeTests
{
    [Fact]
    public async Task GetRangeAggregate_EmptyDb_ReturnsEmpty()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        var result = await service.GetRangeAggregateAsync(new DateOnly(2026, 2, 6), new DateOnly(2026, 2, 7));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRangeAggregate_UsesSnapshotBeforeRangeAsBaseline()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 5, 23, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc)).WithCoffee(103).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc)).WithCoffee(107).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetRangeAggregateAsync(new DateOnly(2026, 2, 6), new DateOnly(2026, 2, 7));

        Assert.Equal(2, result.Count);
        Assert.Equal("2026-02-06", result[0].Date);
        Assert.Equal(3, result[0].CoffeeCount);
        Assert.Equal(4, result[1].CoffeeCount);
    }

    [Fact]
    public async Task GetRangeAggregate_WithoutBaseline_FirstDayCountsFromItsOwnFirstSnapshot()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 18, 0, 0, DateTimeKind.Utc)).WithCoffee(104).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetRangeAggregateAsync(new DateOnly(2026, 2, 6), new DateOnly(2026, 2, 6));

        Assert.Single(result);
        Assert.Equal(4, result[0].CoffeeCount);
    }

    [Fact]
    public async Task GetRangeAggregate_SumsCoffeeAndMilkIntoTotal()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 5, 23, 0, 0, DateTimeKind.Utc))
                .WithCoffee(100).WithMilk(10).WithCoffeeAndMilk(5).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc))
                .WithCoffee(102).WithMilk(11).WithCoffeeAndMilk(7).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetRangeAggregateAsync(new DateOnly(2026, 2, 6), new DateOnly(2026, 2, 6));

        Assert.Single(result);
        Assert.Equal(2, result[0].CoffeeCount);
        Assert.Equal(3, result[0].MilkCount);
        Assert.Equal(5, result[0].Total);
    }

    [Fact]
    public async Task GetRangeAggregate_WithTimezone_GroupsByLocalDate()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 12, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 23, 30, 0, DateTimeKind.Utc)).WithCoffee(102).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetRangeAggregateAsync(new DateOnly(2026, 2, 6), new DateOnly(2026, 2, 7), tzOffsetMinutes: 60);

        Assert.Equal(2, result.Count);
        Assert.Equal("2026-02-06", result[0].Date);
        Assert.Equal("2026-02-07", result[1].Date);
        Assert.Equal(2, result[1].CoffeeCount);
    }

    [Fact]
    public async Task GetRangeAggregate_CounterDrop_ClampsToZero()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Statistics(db);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 5, 23, 0, 0, DateTimeKind.Utc)).WithCoffee(500).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc)).WithCoffee(3).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetRangeAggregateAsync(new DateOnly(2026, 2, 6), new DateOnly(2026, 2, 6));

        Assert.Single(result);
        Assert.Equal(0, result[0].CoffeeCount);
        Assert.Equal(0, result[0].Total);
    }
}
