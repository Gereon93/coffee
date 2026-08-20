using CoffeeApi.Controllers;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeTest.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTest.Controllers;

public class BeanHoppersControllerTests
{
    private static readonly DateTime Morning = new(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Noon = new(2026, 2, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SetBeanHopper_ValidCorrection_ReturnsNoContentAndPersists()
    {
        using var db = TestDbContextFactory.Create();
        var (controller, snapshots) = await CreateAsync(db);

        var result = await controller.SetBeanHopper(
            snapshots[1].Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 2 });

        Assert.IsType<NoContentResult>(result);
        var stored = Assert.Single(db.BeanHopperOverrides);
        Assert.Equal(2, stored.BeanHopper);
    }

    [Fact]
    public async Task SetBeanHopper_UnknownCounter_ReturnsBadRequest()
    {
        using var db = TestDbContextFactory.Create();
        var (controller, snapshots) = await CreateAsync(db);

        var result = await controller.SetBeanHopper(
            snapshots[1].Id,
            new SetBeanHopperDto { Counter = "espresso", BeanHopper = 1 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SetBeanHopper_HopperOutOfRange_ReturnsBadRequest()
    {
        using var db = TestDbContextFactory.Create();
        var (controller, snapshots) = await CreateAsync(db);

        var result = await controller.SetBeanHopper(
            snapshots[1].Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 3 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SetBeanHopper_CounterDidNotMove_ReturnsBadRequest()
    {
        using var db = TestDbContextFactory.Create();
        var (controller, snapshots) = await CreateAsync(db);

        var result = await controller.SetBeanHopper(
            snapshots[1].Id,
            new SetBeanHopperDto { Counter = BeanCounters.CoffeeAndMilk, BeanHopper = 1 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SetBeanHopper_UnknownSnapshot_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var (controller, _) = await CreateAsync(db);

        var result = await controller.SetBeanHopper(
            4711,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 1 });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ClearBeanHopper_ExistingOverride_ReturnsNoContentAndRemovesRow()
    {
        using var db = TestDbContextFactory.Create();
        var (controller, snapshots) = await CreateAsync(db);
        await controller.SetBeanHopper(
            snapshots[1].Id,
            new SetBeanHopperDto { Counter = BeanCounters.Coffee, BeanHopper = 2 });

        var result = await controller.ClearBeanHopper(snapshots[1].Id, BeanCounters.Coffee);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.BeanHopperOverrides);
    }

    [Fact]
    public async Task ClearBeanHopper_WithoutStoredOverride_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var (controller, snapshots) = await CreateAsync(db);

        var result = await controller.ClearBeanHopper(snapshots[1].Id, BeanCounters.Coffee);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ClearBeanHopper_UnknownCounter_ReturnsBadRequest()
    {
        using var db = TestDbContextFactory.Create();
        var (controller, snapshots) = await CreateAsync(db);

        var result = await controller.ClearBeanHopper(snapshots[1].Id, "espresso");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static async Task<(BeanHoppersController Controller, List<MachineSnapshot> Snapshots)> CreateAsync(AppDbContext db)
    {
        var snapshots = new List<MachineSnapshot>
        {
            new SnapshotBuilder().At(Morning).WithCoffee(100).Build(),
            new SnapshotBuilder().At(Noon).WithCoffee(102).Build()
        };
        db.MachineSnapshots.AddRange(snapshots);
        await db.SaveChangesAsync();

        return (new BeanHoppersController(SnapshotServices.BeanHoppers(db)), snapshots);
    }
}
