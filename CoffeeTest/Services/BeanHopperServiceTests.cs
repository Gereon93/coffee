using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeApi.Services;
using CoffeeTest.Helpers;

namespace CoffeeTest.Services;

public class BeanHopperServiceTests
{
    private static readonly DateTime Morning = new(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Noon = new(2026, 2, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetUsage_CoffeeDelta_DefaultsToHopperOne()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());

        var usage = await SnapshotServices.BeanHoppers(db).GetUsageAsync(sequence);

        var entry = Assert.Single(usage[sequence[1].Id]);
        Assert.Equal(BeanCounters.Coffee, entry.Counter);
        Assert.Equal(2, entry.Count);
        Assert.Equal(1, entry.BeanHopper);
        Assert.Equal(BeanHopperSources.Auto, entry.Source);
    }

    [Fact]
    public async Task GetUsage_CoffeeAndMilkDelta_DefaultsToHopperTwo()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffeeAndMilk(40).Build(),
            new SnapshotBuilder().At(Noon).WithCoffeeAndMilk(43).Build());

        var usage = await SnapshotServices.BeanHoppers(db).GetUsageAsync(sequence);

        var entry = Assert.Single(usage[sequence[1].Id]);
        Assert.Equal(BeanCounters.CoffeeAndMilk, entry.Counter);
        Assert.Equal(3, entry.Count);
        Assert.Equal(2, entry.BeanHopper);
    }

    [Fact]
    public async Task GetUsage_MilkAndHotWaterOnly_ProducesNoBeanDraw()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithMilk(5).WithHotWaterCups(3).Build(),
            new SnapshotBuilder().At(Noon).WithMilk(7).WithHotWaterCups(6).Build());

        var usage = await SnapshotServices.BeanHoppers(db).GetUsageAsync(sequence);

        Assert.Empty(usage);
    }

    [Fact]
    public async Task GetUsage_MixedDelta_SplitsPerCounter()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).WithCoffeeAndMilk(40).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).WithCoffeeAndMilk(41).Build());

        var usage = await SnapshotServices.BeanHoppers(db).GetUsageAsync(sequence);

        var entries = usage[sequence[1].Id];
        Assert.Equal(2, entries.Count);
        var coffee = entries.Single(e => e.Counter == BeanCounters.Coffee);
        Assert.Equal(2, coffee.Count);
        Assert.Equal(1, coffee.BeanHopper);

        var coffeeAndMilk = entries.Single(e => e.Counter == BeanCounters.CoffeeAndMilk);
        Assert.Equal(1, coffeeAndMilk.Count);
        Assert.Equal(2, coffeeAndMilk.BeanHopper);
    }

    [Fact]
    public async Task GetUsage_FirstSnapshotOfSequence_HasNoDelta()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());

        var usage = await SnapshotServices.BeanHoppers(db).GetUsageAsync(sequence);

        Assert.False(usage.ContainsKey(sequence[0].Id));
    }

    [Fact]
    public async Task GetUsage_CounterReset_ReadsAsZero()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(3).Build());

        var usage = await SnapshotServices.BeanHoppers(db).GetUsageAsync(sequence);

        Assert.Empty(usage);
    }

    [Fact]
    public async Task SetOverride_MixedDelta_MovesOnlyTheNamedCounter()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).WithCoffeeAndMilk(40).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).WithCoffeeAndMilk(41).Build());
        var service = SnapshotServices.BeanHoppers(db);

        var (success, error, _) = await service.SetOverrideAsync(
            sequence[1].Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 2 });

        Assert.True(success);
        Assert.Equal(BeanHopperError.None, error);

        var entries = (await service.GetUsageAsync(sequence))[sequence[1].Id];
        var coffee = entries.Single(e => e.Counter == BeanCounters.Coffee);
        Assert.Equal(2, coffee.BeanHopper);
        Assert.Equal(BeanHopperSources.Manual, coffee.Source);

        var coffeeAndMilk = entries.Single(e => e.Counter == BeanCounters.CoffeeAndMilk);
        Assert.Equal(2, coffeeAndMilk.BeanHopper);
        Assert.Equal(BeanHopperSources.Auto, coffeeAndMilk.Source);
    }

    [Fact]
    public async Task SetOverride_NullHopper_TakesDrawOutOfBeanAccounting()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());
        var service = SnapshotServices.BeanHoppers(db);

        await service.SetOverrideAsync(
            sequence[1].Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = null });

        var totals = await service.GetTotalsAsync(sequence);
        Assert.Equal(0, totals.Hopper1);
        Assert.Equal(0, totals.Hopper2);
        Assert.Equal(2, totals.Excluded);
    }

    [Fact]
    public async Task SetOverride_Twice_RefreshesUpdatedAt()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());
        var service = SnapshotServices.BeanHoppers(db);

        await service.SetOverrideAsync(sequence[1].Id, new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 2 });

        var backdated = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.BeanHopperOverrides.Single().UpdatedAt = backdated;
        await db.SaveChangesAsync();

        await service.SetOverrideAsync(sequence[1].Id, new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 1 });

        Assert.True(db.BeanHopperOverrides.Single().UpdatedAt > backdated);
    }

    [Fact]
    public async Task SetOverride_Twice_KeepsOneRowAndTakesTheLastValue()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());
        var service = SnapshotServices.BeanHoppers(db);

        await service.SetOverrideAsync(sequence[1].Id, new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 2 });
        await service.SetOverrideAsync(sequence[1].Id, new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = null });

        Assert.Single(db.BeanHopperOverrides);
        var totals = await service.GetTotalsAsync(sequence);
        Assert.Equal(2, totals.Excluded);
    }

    [Theory]
    [InlineData("espresso")]
    [InlineData("Coffee")]
    [InlineData("")]
    public async Task SetOverride_UnknownCounter_IsRejected(string counter)
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());

        var (success, error, _) = await SnapshotServices.BeanHoppers(db).SetOverrideAsync(
            sequence[1].Id,
            new SetBeanHopperDto { Counter = counter, BeanHopper = 1 });

        Assert.False(success);
        Assert.Equal(BeanHopperError.InvalidCounter, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task SetOverride_HopperOutOfRange_IsRejected(int hopper)
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());

        var (success, error, _) = await SnapshotServices.BeanHoppers(db).SetOverrideAsync(
            sequence[1].Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = hopper });

        Assert.False(success);
        Assert.Equal(BeanHopperError.InvalidHopper, error);
    }

    [Fact]
    public async Task SetOverride_UnknownSnapshot_IsRejected()
    {
        using var db = TestDbContextFactory.Create();

        var (success, error, _) = await SnapshotServices.BeanHoppers(db).SetOverrideAsync(
            4711,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 1 });

        Assert.False(success);
        Assert.Equal(BeanHopperError.SnapshotNotFound, error);
    }

    [Fact]
    public async Task SetOverride_CounterDidNotMove_IsRejected()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());

        var (success, error, _) = await SnapshotServices.BeanHoppers(db).SetOverrideAsync(
            sequence[1].Id,
            new SetBeanHopperDto { Counter = BeanCounters.CoffeeAndMilk, BeanHopper = 1 });

        Assert.False(success);
        Assert.Equal(BeanHopperError.NoConsumption, error);
        Assert.Empty(db.BeanHopperOverrides);
    }

    [Fact]
    public async Task SetOverride_FirstSnapshotEver_IsRejected()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db, new SnapshotBuilder().At(Morning).WithCoffee(100).Build());

        var (success, error, _) = await SnapshotServices.BeanHoppers(db).SetOverrideAsync(
            sequence[0].Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 2 });

        Assert.False(success);
        Assert.Equal(BeanHopperError.NoConsumption, error);
    }

    [Fact]
    public async Task ClearOverride_RestoresTheAutomaticRule()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());
        var service = SnapshotServices.BeanHoppers(db);
        await service.SetOverrideAsync(sequence[1].Id, new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 2 });

        var (success, error, _) = await service.ClearOverrideAsync(sequence[1].Id, BeanCounters.Coffee);

        Assert.True(success);
        Assert.Equal(BeanHopperError.None, error);

        var entry = Assert.Single((await service.GetUsageAsync(sequence))[sequence[1].Id]);
        Assert.Equal(1, entry.BeanHopper);
        Assert.Equal(BeanHopperSources.Auto, entry.Source);
    }

    [Fact]
    public async Task ClearOverride_WithoutStoredOverride_IsRejected()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build());

        var (success, error, _) = await SnapshotServices.BeanHoppers(db)
            .ClearOverrideAsync(sequence[1].Id, BeanCounters.Coffee);

        Assert.False(success);
        Assert.Equal(BeanHopperError.OverrideNotFound, error);
    }

    [Fact]
    public async Task ClearOverride_UnknownCounter_IsRejected()
    {
        using var db = TestDbContextFactory.Create();

        var (success, error, _) = await SnapshotServices.BeanHoppers(db).ClearOverrideAsync(1, "espresso");

        Assert.False(success);
        Assert.Equal(BeanHopperError.InvalidCounter, error);
    }

    [Fact]
    public async Task GetTotals_SumsBothHoppersAcrossSnapshots()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db,
            new SnapshotBuilder().At(Morning).WithCoffee(100).WithCoffeeAndMilk(40).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).WithCoffeeAndMilk(41).Build(),
            new SnapshotBuilder().At(Noon.AddHours(4)).WithCoffee(103).WithCoffeeAndMilk(44).Build());

        var totals = await SnapshotServices.BeanHoppers(db).GetTotalsAsync(sequence);

        Assert.Equal(3, totals.Hopper1);
        Assert.Equal(4, totals.Hopper2);
        Assert.Equal(0, totals.Excluded);
    }

    [Fact]
    public async Task GetTotals_SingleSnapshot_IsAllZero()
    {
        using var db = TestDbContextFactory.Create();
        var sequence = await SeedAsync(db, new SnapshotBuilder().At(Morning).WithCoffee(100).Build());

        var totals = await SnapshotServices.BeanHoppers(db).GetTotalsAsync(sequence);

        Assert.Equal(0, totals.Hopper1);
        Assert.Equal(0, totals.Hopper2);
        Assert.Equal(0, totals.Excluded);
    }

    private static async Task<List<MachineSnapshot>> SeedAsync(AppDbContext db, params MachineSnapshot[] snapshots)
    {
        db.MachineSnapshots.AddRange(snapshots);
        await db.SaveChangesAsync();
        return snapshots.ToList();
    }
}
