using CoffeeApi.Domain;
using CoffeeApi.Infrastructure;
using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Services;

public class HistoricalBackfillServiceTests
{
    [Fact]
    public async Task Preview_UsesFirstRealSnapshotAndReturnsDailyPlan()
    {
        using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.Add(FirstRealSnapshot());
        await db.SaveChangesAsync();
        var service = new HistoricalBackfillService(db, NullLogger<HistoricalBackfillService>.Instance);

        var preview = await service.PreviewAsync("2025-07-10");

        Assert.True(preview.Success);
        Assert.NotNull(preview.Plan);
        Assert.Equal(1, preview.Plan!.FirstSnapshotId);
        Assert.Equal(new DateOnly(2025, 12, 31), preview.Plan.EndDate);
        Assert.Equal(175, preview.Plan.EstimatedSnapshotCount);
        Assert.Equal(1009, preview.Plan.TargetBeverageCount);
    }

    [Fact]
    public async Task Apply_CreatesEstimatedRowsWithExactTargetAndIsIdempotent()
    {
        using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.Add(FirstRealSnapshot());
        await db.SaveChangesAsync();
        var service = new HistoricalBackfillService(db, NullLogger<HistoricalBackfillService>.Instance);

        var applied = await service.ApplyAsync("2025-07-10");
        var secondAttempt = await service.ApplyAsync("2025-07-10");

        Assert.True(applied.Success);
        Assert.False(secondAttempt.Success);
        var estimated = await db.MachineSnapshots.Where(s => s.IsEstimated).ToListAsync();
        Assert.Equal(175, estimated.Count);
        Assert.Equal(0, estimated.Min(s => s.TotalBeverages));
        Assert.Equal(1010, estimated.Max(s => s.TotalBeverages));
        Assert.Equal(988, estimated.Max(s => s.BeverageCounterCoffee));
        Assert.Equal(10, estimated.Max(s => s.BeverageCounterCoffeeAndMilk));
        Assert.Equal(11, estimated.Max(s => s.BeverageCounterMilk));
    }

    [Fact]
    public async Task Preview_UsesBerlinCalendarYearForUtcBoundary()
    {
        using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.Add(FirstRealSnapshot(
            new DateTime(2025, 12, 31, 23, 30, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();
        var service = new HistoricalBackfillService(db, NullLogger<HistoricalBackfillService>.Instance);

        var preview = await service.PreviewAsync("2025-07-10");

        Assert.True(preview.Success);
        Assert.Equal(new DateOnly(2025, 12, 31), preview.Plan!.EndDate);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2025-12-31")]
    [InlineData("0001-01-01")]
    public async Task Preview_RejectsInvalidOrUnsafePeriodsWithoutWriting(string commissionedAt)
    {
        using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.Add(FirstRealSnapshot());
        await db.SaveChangesAsync();
        var service = new HistoricalBackfillService(db, NullLogger<HistoricalBackfillService>.Instance);

        var preview = await service.PreviewAsync(commissionedAt);

        Assert.False(preview.Success);
        Assert.Empty(await db.MachineSnapshots.Where(snapshot => snapshot.IsEstimated).ToListAsync());
    }

    [Fact]
    public async Task Preview_RejectsWhenNoRealSnapshotExists()
    {
        using var db = TestDbContextFactory.Create();
        var service = new HistoricalBackfillService(db, NullLogger<HistoricalBackfillService>.Instance);

        var preview = await service.PreviewAsync("2025-07-10");

        Assert.False(preview.Success);
        Assert.Empty(await db.MachineSnapshots.Where(snapshot => snapshot.IsEstimated).ToListAsync());
    }

    private static MachineSnapshot FirstRealSnapshot(
        DateTime timestamp = default) => new()
    {
        Id = 1,
        Timestamp = timestamp == default
            ? new DateTime(2026, 1, 25, 16, 10, 0, DateTimeKind.Utc)
            : timestamp,
        BeverageCounterCoffee = 988,
        BeverageCounterCoffeeAndMilk = 10,
        BeverageCounterMilk = 11,
        BeverageCounterHotWaterCups = 1,
        BeverageCounterHotWater = 150,
        OperationState = "Ready"
    };
}
