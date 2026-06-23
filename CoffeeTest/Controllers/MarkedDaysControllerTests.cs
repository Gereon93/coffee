using CoffeeApi.Controllers;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Controllers;

public class MarkedDaysControllerTests
{
    private static MarkedDaysController CreateController(AppDbContext db)
    {
        var service = new MarkedDayService(db, NullLogger<MarkedDayService>.Instance);
        return new MarkedDaysController(service);
    }

    [Fact]
    public async Task GetAll_EmptyDb_ReturnsEmptyList()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<MarkedDayDto>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetAll_FilterByKind_OnlyReturnsMatching()
    {
        using var db = TestDbContextFactory.Create();
        db.MarkedDays.AddRange(
            new MarkedDay { Date = new DateOnly(2026, 2, 15), Kind = "mass-import", Reason = "x", CreatedAt = DateTime.UtcNow },
            new MarkedDay { Date = new DateOnly(2026, 2, 16), Kind = "event", EventType = "birthday", Reason = "y", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.GetAll(kind: "event");

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<MarkedDayDto>>(ok.Value);
        var only = Assert.Single(list);
        Assert.Equal("event", only.Kind);
        Assert.Equal("birthday", only.EventType);
    }

    [Fact]
    public async Task GetAll_InvalidKind_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.GetAll(kind: "wat");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_MassImport_Returns201AndPersists()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-02-15", Kind = "mass-import", Reason = "BSH API outage" };

        var result = await controller.Create(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var returned = Assert.IsType<MarkedDayDto>(created.Value);
        Assert.Equal("mass-import", returned.Kind);
        Assert.Null(returned.EventType);

        var row = Assert.Single(await db.MarkedDays.ToListAsync());
        Assert.Equal("mass-import", row.Kind);
    }

    [Fact]
    public async Task Create_Event_Returns201WithEventType()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto
        {
            Date = "2026-04-22",
            Kind = "event",
            EventType = "birthday",
            Reason = "Schwiegereltern"
        };

        var result = await controller.Create(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var returned = Assert.IsType<MarkedDayDto>(created.Value);
        Assert.Equal("event", returned.Kind);
        Assert.Equal("birthday", returned.EventType);
        Assert.Equal("Schwiegereltern", returned.Reason);
    }

    [Fact]
    public async Task Create_EventWithoutEventType_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-04-22", Kind = "event", EventType = null, Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_EventWithInvalidEventType_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-04-22", Kind = "event", EventType = "alien-abduction", Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_InvalidKind_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-04-22", Kind = "weird", Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_EventWithEmptyReason_OkBecauseEventTypeIsEnough()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-04-22", Kind = "event", EventType = "visitors", Reason = "" };

        var result = await controller.Create(dto);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Create_MassImportEmptyReason_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-02-15", Kind = "mass-import", Reason = "" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_InvalidDate_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "not-a-date", Kind = "mass-import", Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_ExistingDate_Returns409()
    {
        using var db = TestDbContextFactory.Create();
        db.MarkedDays.Add(new MarkedDay { Date = new DateOnly(2026, 2, 15), Kind = "mass-import", Reason = "first", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-02-15", Kind = "event", EventType = "birthday", Reason = "second" };

        var result = await controller.Create(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Delete_Existing_Returns204AndRemoves()
    {
        using var db = TestDbContextFactory.Create();
        db.MarkedDays.Add(new MarkedDay { Date = new DateOnly(2026, 2, 15), Kind = "mass-import", Reason = "x", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Delete("2026-02-15");

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.MarkedDays.ToListAsync());
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.Delete("2026-02-15");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Delete_InvalidDate_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.Delete("bad-date");

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
