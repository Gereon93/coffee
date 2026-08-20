using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeTest.Helpers;

namespace CoffeeTest.Services;

/// <summary>
/// Aggregation of bean draws per hopper, on top of the existing daily and
/// range figures.
/// </summary>
public class SnapshotStatisticsBeanHopperTests
{
    private static readonly DateOnly Day = new(2026, 2, 7);

    [Fact]
    public async Task GetDailySummary_NoSnapshots_HasEmptyHopperTotals()
    {
        using var db = TestDbContextFactory.Create();

        var result = await SnapshotServices.Statistics(db).GetDailySummaryAsync(Day);

        Assert.Equal(0, result.BeanHoppers.Hopper1);
        Assert.Equal(0, result.BeanHoppers.Hopper2);
        Assert.Equal(0, result.BeanHoppers.Excluded);
    }

    [Fact]
    public async Task GetDailySummary_SplitsDrawsByDefaultRules()
    {
        using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 23, 0, 0, DateTimeKind.Utc)).WithCoffee(100).WithCoffeeAndMilk(40).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(102).WithCoffeeAndMilk(40).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 14, 0, 0, DateTimeKind.Utc)).WithCoffee(103).WithCoffeeAndMilk(43).Build()
        );
        await db.SaveChangesAsync();

        var result = await SnapshotServices.Statistics(db).GetDailySummaryAsync(Day);

        Assert.Equal(3, result.BeanHoppers.Hopper1);
        Assert.Equal(3, result.BeanHoppers.Hopper2);
        Assert.Equal(0, result.BeanHoppers.Excluded);
    }

    [Fact]
    public async Task GetDailySummary_HotWaterAndMilk_DoNotCountAsBeanDraw()
    {
        using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 23, 0, 0, DateTimeKind.Utc)).WithMilk(5).WithHotWaterCups(3).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithMilk(8).WithHotWaterCups(9).Build()
        );
        await db.SaveChangesAsync();

        var result = await SnapshotServices.Statistics(db).GetDailySummaryAsync(Day);

        Assert.Equal(3, result.MilkDrinksToday);
        Assert.Equal(0, result.BeanHoppers.Hopper1);
        Assert.Equal(0, result.BeanHoppers.Hopper2);
    }

    [Fact]
    public async Task GetDailySummary_ManualOverride_MovesDrawToOtherHopper()
    {
        using var db = TestDbContextFactory.Create();
        var corrected = new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(102).Build();
        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 23, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            corrected
        );
        await db.SaveChangesAsync();

        await SnapshotServices.BeanHoppers(db).SetOverrideAsync(
            corrected.Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 2 });

        var result = await SnapshotServices.Statistics(db).GetDailySummaryAsync(Day);

        Assert.Equal(0, result.BeanHoppers.Hopper1);
        Assert.Equal(2, result.BeanHoppers.Hopper2);
        Assert.Equal(2, result.CoffeeToday);
    }

    [Fact]
    public async Task GetRangeAggregate_SplitsEachDaySeparately()
    {
        using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(100).WithCoffeeAndMilk(40).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 18, 0, 0, DateTimeKind.Utc)).WithCoffee(103).WithCoffeeAndMilk(41).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 8, 9, 0, 0, DateTimeKind.Utc)).WithCoffee(104).WithCoffeeAndMilk(45).Build()
        );
        await db.SaveChangesAsync();

        var result = await SnapshotServices.Statistics(db)
            .GetRangeAggregateAsync(Day, new DateOnly(2026, 2, 8));

        Assert.Equal(2, result.Count);

        Assert.Equal(3, result[0].BeanHoppers.Hopper1);
        Assert.Equal(1, result[0].BeanHoppers.Hopper2);

        Assert.Equal(1, result[1].BeanHoppers.Hopper1);
        Assert.Equal(4, result[1].BeanHoppers.Hopper2);
    }

    [Fact]
    public async Task GetRangeAggregate_OverrideCountsOnItsOwnDayOnly()
    {
        using var db = TestDbContextFactory.Create();
        var corrected = new SnapshotBuilder().At(new DateTime(2026, 2, 8, 9, 0, 0, DateTimeKind.Utc)).WithCoffee(104).Build();
        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 18, 0, 0, DateTimeKind.Utc)).WithCoffee(103).Build(),
            corrected
        );
        await db.SaveChangesAsync();

        await SnapshotServices.BeanHoppers(db).SetOverrideAsync(
            corrected.Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = null });

        var result = await SnapshotServices.Statistics(db)
            .GetRangeAggregateAsync(Day, new DateOnly(2026, 2, 8));

        Assert.Equal(3, result[0].BeanHoppers.Hopper1);
        Assert.Equal(0, result[0].BeanHoppers.Excluded);

        Assert.Equal(0, result[1].BeanHoppers.Hopper1);
        Assert.Equal(1, result[1].BeanHoppers.Excluded);
    }
}
