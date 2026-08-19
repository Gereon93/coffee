using CoffeeApi.Controllers;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeTest.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTest.Controllers;

public class StatsControllerTests
{
    private static (StatsController Controller, AppDbContext Db) Create(string? dbName = null)
    {
        var db = TestDbContextFactory.Create(dbName);
        var controller = new StatsController(SnapshotServices.Query(db), SnapshotServices.Statistics(db));
        return (controller, db);
    }

    [Fact]
    public async Task GetAll_EmptyDb_ReturnsEmptyList()
    {
        var (controller, _) = Create();

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PaginatedResponseDto<SnapshotResponseDto>>(ok.Value);
        Assert.Empty(response.Data);
        Assert.Equal(0, response.Pagination.TotalItems);
    }

    [Theory]
    [InlineData("02/07/2026")]
    [InlineData("7.2.2026")]
    [InlineData("2026-2-7")]
    public async Task GetDaily_NonCanonicalDate_ReturnsBadRequest(string date)
    {
        var (controller, _) = Create();

        var result = await controller.GetDaily(date);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("02/07/2026", "2026-02-08")]
    [InlineData("2026-02-07", "02/08/2026")]
    public async Task GetRange_NonCanonicalDate_ReturnsBadRequest(string from, string to)
    {
        var (controller, _) = Create();

        var result = await controller.GetRange(from, to);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Health_ReportsDatabaseConnected()
    {
        var (controller, _) = Create();

        var result = await controller.Health();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<HealthResponseDto>(ok.Value);
        Assert.Equal("connected", response.Database);
    }

    [Fact]
    public async Task GetDaily_InvalidDate_ReturnsBadRequest()
    {
        var (controller, _) = Create();

        var result = await controller.GetDaily("not-a-date");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetDaily_ValidDate_ReturnsDailyStats()
    {
        var (controller, _) = Create();

        var result = await controller.GetDaily("2026-02-07");

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DailyStatsResponseDto>(ok.Value);
        Assert.Equal("2026-02-07", response.Date);
    }

    [Fact]
    public async Task GetRange_InvalidDates_ReturnsBadRequest()
    {
        var (controller, _) = Create();

        var result = await controller.GetRange("bad", "dates");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetRange_CrossDayDelta_CalculatesCorrectly()
    {
        var (controller, db) = Create();

        // Day before range (baseline)
        db.MachineSnapshots.Add(
            new SnapshotBuilder().At(new DateTime(2026, 2, 5, 23, 0, 0, DateTimeKind.Utc)).WithCoffee(100).Build()
        );
        // Feb 6: single snapshot
        db.MachineSnapshots.Add(
            new SnapshotBuilder().At(new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc)).WithCoffee(103).Build()
        );
        // Feb 7: single snapshot
        db.MachineSnapshots.Add(
            new SnapshotBuilder().At(new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc)).WithCoffee(107).Build()
        );
        await db.SaveChangesAsync();

        var result = await controller.GetRange("2026-02-06", "2026-02-07");

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RangeStatsResponseDto>(ok.Value);

        Assert.Equal(2, response.Data.Count);
        Assert.Equal(3, response.Data[0].CoffeeCount);  // 103 - 100
        Assert.Equal(4, response.Data[1].CoffeeCount);  // 107 - 103
    }

    [Fact]
    public async Task GetHeatmap_CapsWeeksAt52()
    {
        var (controller, _) = Create();

        var result = await controller.GetHeatmap(100);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<HeatmapResponseDto>(ok.Value);
        Assert.Equal(52, response.Weeks);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var (controller, _) = Create();

        var result = await controller.Health();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<HealthResponseDto>(ok.Value);
        Assert.Equal("healthy", response.Status);
    }
}
