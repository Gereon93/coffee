using CoffeeApi.Controllers;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeTest.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Controllers;

public class ExcludedDaysControllerTests
{
    private static ExcludedDaysController CreateController(AppDbContext db)
    {
        return new ExcludedDaysController(db, NullLogger<ExcludedDaysController>.Instance);
    }

    [Fact]
    public async Task GetAll_EmptyDb_ReturnsEmptyList()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ExcludedDayDto>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Create_ValidPayload_Returns201AndPersists()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateExcludedDayDto { Date = "2026-02-15", Reason = "BSH API outage" };

        var result = await controller.Create(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var returned = Assert.IsType<ExcludedDayDto>(created.Value);
        Assert.Equal("2026-02-15", returned.Date);
        Assert.Equal("BSH API outage", returned.Reason);

        var row = Assert.Single(await db.ExcludedDays.ToListAsync());
        Assert.Equal(new DateOnly(2026, 2, 15), row.Date);
    }

    [Fact]
    public async Task Create_InvalidDate_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateExcludedDayDto { Date = "not-a-date", Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_EmptyReason_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateExcludedDayDto { Date = "2026-02-15", Reason = "" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_ExistingDate_Returns409()
    {
        using var db = TestDbContextFactory.Create();
        db.ExcludedDays.Add(new ExcludedDay { Date = new DateOnly(2026, 2, 15), Reason = "first", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        var dto = new CreateExcludedDayDto { Date = "2026-02-15", Reason = "second" };

        var result = await controller.Create(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Delete_Existing_Returns204AndRemoves()
    {
        using var db = TestDbContextFactory.Create();
        db.ExcludedDays.Add(new ExcludedDay { Date = new DateOnly(2026, 2, 15), Reason = "x", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Delete("2026-02-15");

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.ExcludedDays.ToListAsync());
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
