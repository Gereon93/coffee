using CoffeeApi.Domain;
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
    public async Task GetHeatmapData_SkipsDeltasOnExcludedDays()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        // Friday 20:00 baseline, Friday 21:00 mass-import spike (+30),
        // Saturday 08:00 normal delta (+1). Friday is marked as excluded.
        var friday = DateTime.UtcNow.Date;
        while (friday.DayOfWeek != DayOfWeek.Friday) friday = friday.AddDays(-1);
        var saturday = friday.AddDays(1);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(friday.AddHours(20)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(friday.AddHours(21)).WithCoffee(130).Build(),
            new SnapshotBuilder().At(saturday.AddHours(8)).WithCoffee(131).Build()
        );
        db.ExcludedDays.Add(new ExcludedDay
        {
            Date = DateOnly.FromDateTime(friday),
            Reason = "mass import",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await service.GetHeatmapDataAsync(4);

        // Friday bucket (day 5, hour 21) must be absent; Saturday bucket (day 6, hour 8) stays.
        Assert.DoesNotContain(result, h => h.DayOfWeek == 5 && h.Hour == 21);
        Assert.Contains(result, h => h.DayOfWeek == 6 && h.Hour == 8 && h.Count == 1);
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
