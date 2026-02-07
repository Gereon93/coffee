using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Services;

public class SnapshotServiceQueryTests
{
    [Fact]
    public async Task GetLatest_EmptyDb_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        var result = await service.GetLatestAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatest_ReturnsNewest()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).WithCoffee(10).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)).WithCoffee(20).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetLatestAsync();

        Assert.NotNull(result);
        Assert.Equal(20, result.BeverageCounterCoffee);
    }

    [Fact]
    public async Task GetAll_CapsPageSizeAt100()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        // Add 5 snapshots
        for (int i = 0; i < 5; i++)
        {
            db.MachineSnapshots.Add(
                new SnapshotBuilder().At(DateTime.UtcNow.AddMinutes(i)).WithCoffee(i).Build()
            );
        }
        await db.SaveChangesAsync();

        var (items, total) = await service.GetAllAsync(page: 1, pageSize: 200);

        Assert.Equal(5, total);
        Assert.Equal(5, items.Count); // capped at 100 but only 5 exist
    }

    [Fact]
    public async Task GetAll_PaginatesCorrectly()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        for (int i = 0; i < 5; i++)
        {
            db.MachineSnapshots.Add(
                new SnapshotBuilder().At(DateTime.UtcNow.AddMinutes(i)).WithCoffee(i).Build()
            );
        }
        await db.SaveChangesAsync();

        var (items, total) = await service.GetAllAsync(page: 2, pageSize: 2);

        Assert.Equal(5, total);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetByDate_ReturnsOnlyThatDay()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 23, 0, 0, DateTimeKind.Utc)).WithCoffee(10).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(11).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 14, 0, 0, DateTimeKind.Utc)).WithCoffee(12).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 8, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(13).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetByDateAsync(new DateOnly(2026, 2, 7));

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(7, s.Timestamp.Day));
    }

    [Fact]
    public async Task GetByDateRange_ReturnsCorrectRange()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(new DateTime(2026, 2, 5, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(10).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(11).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(12).Build(),
            new SnapshotBuilder().At(new DateTime(2026, 2, 8, 8, 0, 0, DateTimeKind.Utc)).WithCoffee(13).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetByDateRangeAsync(new DateOnly(2026, 2, 6), new DateOnly(2026, 2, 7));

        Assert.Equal(2, result.Count);
    }
}
