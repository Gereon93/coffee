using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Services;

public class SnapshotServiceHeatmapTests
{
    [Fact]
    public async Task GetHeatmapData_NoSnapshots_ReturnsEmpty()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        var result = await service.GetHeatmapDataAsync(4);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetHeatmapData_GroupsByDayAndHour()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        // Monday 10:00 -> Monday 11:00 = +2 coffees
        var monday = DateTime.UtcNow.Date;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(monday.AddHours(10)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(monday.AddHours(11)).WithCoffee(102).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetHeatmapDataAsync(4);

        Assert.Single(result);
        Assert.Equal(1, result[0].DayOfWeek); // Monday = 1
        Assert.Equal(11, result[0].Hour);
        Assert.Equal(2, result[0].Count);
    }

    [Fact]
    public async Task GetHeatmapData_SundayIsDayOfWeek7()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        var sunday = DateTime.UtcNow.Date;
        while (sunday.DayOfWeek != DayOfWeek.Sunday) sunday = sunday.AddDays(-1);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(sunday.AddHours(9)).WithCoffee(50).Build(),
            new SnapshotBuilder().At(sunday.AddHours(10)).WithCoffee(51).Build()
        );
        await db.SaveChangesAsync();

        var result = await service.GetHeatmapDataAsync(4);

        Assert.Single(result);
        Assert.Equal(7, result[0].DayOfWeek); // Sunday = 7 (ISO-8601)
    }
}
