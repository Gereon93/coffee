using CoffeeApi.Domain;
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
        await using var db = TestDbContextFactory.Create();
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
        await using var db = TestDbContextFactory.Create();
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
        await using var db = TestDbContextFactory.Create();
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
        await using var db = TestDbContextFactory.Create();
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
        await using var db = TestDbContextFactory.Create();
        var service = new HistoricalBackfillService(db, NullLogger<HistoricalBackfillService>.Instance);

        var preview = await service.PreviewAsync("2025-07-10");

        Assert.False(preview.Success);
        Assert.Empty(await db.MachineSnapshots.Where(snapshot => snapshot.IsEstimated).ToListAsync());
    }

    [Fact]
    public async Task Preview_UsesFirstRealSnapshotOfRequestedMachine()
    {
        await using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.Add(FirstRealSnapshot(machineId: "EQ900-A"));
        db.MachineSnapshots.Add(FirstRealSnapshot(
            new DateTime(2026, 2, 1, 16, 10, 0, DateTimeKind.Utc), "EQ900-B", 2));
        await db.SaveChangesAsync();
        var service = new HistoricalBackfillService(db, NullLogger<HistoricalBackfillService>.Instance);

        var preview = await service.PreviewAsync("2025-07-10", "EQ900-B");

        Assert.True(preview.Success);
        Assert.Equal("EQ900-B", preview.Plan!.MachineId);
        Assert.Equal(2, preview.Plan.FirstSnapshotId);
    }

    [Fact]
    public async Task Apply_AllowsIndependentBackfillsPerMachine()
    {
        await using var db = TestDbContextFactory.Create();
        db.MachineSnapshots.Add(FirstRealSnapshot(machineId: "EQ900-A"));
        db.MachineSnapshots.Add(FirstRealSnapshot(
            new DateTime(2026, 2, 1, 16, 10, 0, DateTimeKind.Utc), "EQ900-B", 2));
        await db.SaveChangesAsync();
        var service = new HistoricalBackfillService(db, NullLogger<HistoricalBackfillService>.Instance);

        var machineA = await service.ApplyAsync("2025-07-10", "EQ900-A");
        var machineB = await service.ApplyAsync("2025-07-10", "EQ900-B");

        Assert.True(machineA.Success);
        Assert.True(machineB.Success);
        Assert.Equal(175, await db.MachineSnapshots.CountAsync(snapshot =>
            snapshot.IsEstimated && snapshot.MachineId == "EQ900-A"));
        Assert.Equal(175, await db.MachineSnapshots.CountAsync(snapshot =>
            snapshot.IsEstimated && snapshot.MachineId == "EQ900-B"));
    }

    private static MachineSnapshot FirstRealSnapshot(
        DateTime timestamp = default,
        string machineId = "EQ900-DEFAULT",
        int id = 1) => new()
    {
        Id = id,
        MachineId = machineId,
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
