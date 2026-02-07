using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Controllers;

/// <summary>
/// Controller for reading EQ900 statistics
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly ISnapshotService _snapshotService;
    private readonly AppDbContext _context;
    private readonly ILogger<StatsController> _logger;

    public StatsController(ISnapshotService snapshotService, AppDbContext context, ILogger<StatsController> logger)
    {
        _snapshotService = snapshotService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all snapshots (paginated)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<SnapshotResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (items, totalCount) = await _snapshotService.GetAllAsync(page, pageSize);

        var response = new PaginatedResponseDto<SnapshotResponseDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Pagination = new PaginationDto
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Get statistics for a specific date
    /// </summary>
    [HttpGet("daily/{date}")]
    [ProducesResponseType(typeof(DailyStatsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDaily(string date)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return BadRequest(new { error = "Invalid date format", details = new[] { "Use yyyy-MM-dd format" } });
        }

        var snapshots = await _snapshotService.GetByDateAsync(parsedDate);
        var summary = await _snapshotService.GetDailySummaryAsync(parsedDate);

        var response = new DailyStatsResponseDto
        {
            Date = date,
            Snapshots = snapshots.Select(MapToDto).ToList(),
            Summary = summary
        };

        return Ok(response);
    }

    /// <summary>
    /// Get statistics for a date range
    /// </summary>
    [HttpGet("range")]
    [ProducesResponseType(typeof(RangeStatsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRange([FromQuery] string from, [FromQuery] string to)
    {
        if (!DateOnly.TryParse(from, out var fromDate) || !DateOnly.TryParse(to, out var toDate))
        {
            return BadRequest(new { error = "Invalid date format", details = new[] { "Use yyyy-MM-dd format for both from and to" } });
        }

        var snapshots = await _snapshotService.GetByDateRangeAsync(fromDate, toDate);

        // Get the last snapshot before the range for cross-day deltas
        var rangeStart = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var previousSnapshot = await _context.MachineSnapshots
            .Where(s => s.Timestamp < rangeStart)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();

        // Aggregate by date with cross-day delta support
        var groups = snapshots
            .GroupBy(s => DateOnly.FromDateTime(s.Timestamp))
            .OrderBy(g => g.Key)
            .ToList();

        Domain.MachineSnapshot? lastPrevious = previousSnapshot;
        var dailyData = new List<DailyAggregateDto>();

        foreach (var g in groups)
        {
            var daySnapshots = g.OrderBy(s => s.Timestamp).ToList();
            var baseline = lastPrevious ?? daySnapshots.First();
            var last = daySnapshots.Last();

            dailyData.Add(new DailyAggregateDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                CoffeeCount = Math.Max(0, last.BeverageCounterCoffee - baseline.BeverageCounterCoffee),
                MilkCount = Math.Max(0,
                    (last.BeverageCounterCoffeeAndMilk - baseline.BeverageCounterCoffeeAndMilk) +
                    (last.BeverageCounterMilk - baseline.BeverageCounterMilk)),
                Total = Math.Max(0, last.TotalBeverages - baseline.TotalBeverages)
            });

            lastPrevious = last;
        }

        var response = new RangeStatsResponseDto
        {
            From = from,
            To = to,
            Data = dailyData
        };

        return Ok(response);
    }

    /// <summary>
    /// Get heatmap data (hour x day of week)
    /// </summary>
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(HeatmapResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeatmap([FromQuery] int weeks = 4)
    {
        weeks = Math.Min(weeks, 52); // Cap at 1 year

        var heatmapData = await _snapshotService.GetHeatmapDataAsync(weeks);

        var response = new HeatmapResponseDto
        {
            Weeks = weeks,
            Heatmap = heatmapData
        };

        return Ok(response);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("/api/health")]
    [ProducesResponseType(typeof(HealthResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Health()
    {
        var lastSnapshot = await _snapshotService.GetLatestAsync();

        var response = new HealthResponseDto
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Database = await _context.Database.CanConnectAsync() ? "connected" : "disconnected",
            LastSnapshot = lastSnapshot?.Timestamp
        };

        return Ok(response);
    }

    private static SnapshotResponseDto MapToDto(Domain.MachineSnapshot snapshot)
    {
        return new SnapshotResponseDto
        {
            Id = snapshot.Id,
            Timestamp = snapshot.Timestamp,
            TotalBeverages = snapshot.TotalBeverages,
            BeverageCounterCoffee = snapshot.BeverageCounterCoffee,
            BeverageCounterCoffeeAndMilk = snapshot.BeverageCounterCoffeeAndMilk,
            BeverageCounterMilk = snapshot.BeverageCounterMilk,
            BeverageCounterHotWaterCups = snapshot.BeverageCounterHotWaterCups,
            BeverageCounterHotWater = snapshot.BeverageCounterHotWater,
            OperationState = snapshot.OperationState
        };
    }
}
