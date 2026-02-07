using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Services;

public class SnapshotServiceDailySummaryTests
{
    [Fact]
    public async Task GetDailySummary_NoSnapshots_ReturnsZeros()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        var result = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7));

        Assert.Equal(0, result.TotalToday);
        Assert.Equal(0, result.CoffeeToday);
        Assert.Equal(0, result.MilkDrinksToday);
        Assert.Null(result.PeakHour);
    }

    [Fact]
    public async Task GetDailySummary_MultipleSnapshots_CalculatesDeltas()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc)).WithCoffee(103).WithMilk(1).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 14, 0, 0, DateTimeKind.Utc)).WithCoffee(105).WithMilk(2).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7));

        Assert.Equal(5, result.CoffeeToday);
        Assert.Equal(2, result.MilkDrinksToday);
        Assert.Equal(7, result.TotalToday);
    }

    [Fact]
    public async Task GetDailySummary_SingleSnapshot_UsesYesterdayAsBaseline()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        // Yesterday's last snapshot
        db.MachineSnapshots.Add(
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 23, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build()
        );
        // Today's only snapshot
        db.MachineSnapshots.Add(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(102).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7));

        Assert.Equal(2, result.CoffeeToday);
        Assert.Equal(2, result.TotalToday);
    }

    [Fact]
    public async Task GetDailySummary_FindsPeakHour_Utc()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 9, 0, 0, DateTimeKind.Utc)).WithCoffee(101).Build(),   // +1
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 14, 0, 0, DateTimeKind.Utc)).WithCoffee(104).Build()  // +3 = peak
        );
        await db.SaveChangesAsync();

        // Without timezone offset → peak hour in UTC
        var result = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7));

        Assert.Equal(14, result.PeakHour);
    }

    [Fact]
    public async Task GetDailySummary_FindsPeakHour_WithTimezone()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 9, 0, 0, DateTimeKind.Utc)).WithCoffee(101).Build(),   // +1
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 14, 0, 0, DateTimeKind.Utc)).WithCoffee(104).Build()  // +3 = peak
        );
        await db.SaveChangesAsync();

        // CET (UTC+1) → peak hour should be 15 instead of 14
        var result = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7), tzOffsetMinutes: 60);

        Assert.Equal(15, result.PeakHour);
    }

    [Fact]
    public async Task GetDailySummary_NoPreviousSnapshot_UsesFirstOfDay()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        // No yesterday data - only today
        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 12, 0, 0, DateTimeKind.Utc)).WithCoffee(105).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7));

        Assert.Equal(5, result.CoffeeToday);
    }

    [Fact]
    public async Task GetDailySummary_TimezoneShiftsDayBoundary()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        // Snapshot at 23:30 UTC = 00:30 CET on Feb 8 (belongs to Feb 8 in CET)
        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 22, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 23, 30, 0, DateTimeKind.Utc)).WithCoffee(102).Build()
        );
        await db.SaveChangesAsync();

        // Query Feb 7 in UTC → both snapshots included
        var resultUtc = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7));
        Assert.Equal(2, resultUtc.CoffeeToday);

        // Query Feb 7 in CET → only the 22:00 UTC snapshot (23:00 CET) is in Feb 7
        // The 23:30 UTC snapshot is 00:30 CET Feb 8, so NOT in Feb 7 CET
        var resultCet = await service.GetDailySummaryAsync(new DateOnly(2026, 2, 7), tzOffsetMinutes: 60);
        Assert.Equal(0, resultCet.TotalToday); // Only one snapshot, no delta possible
    }
}
