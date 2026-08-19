using System.Globalization;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeApi.Controllers;

/// <summary>
/// Controller for reading EQ900 statistics
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Upper bound on the heatmap window — one year.</summary>
    private const int MaxHeatmapWeeks = 52;

    private static readonly string[] PageDetails = ["page must be >= 1"];
    private static readonly string[] PageSizeDetails = ["pageSize must be >= 1"];
    private static readonly string[] DateFormatDetails = ["Use yyyy-MM-dd format"];
    private static readonly string[] RangeFormatDetails = ["Use yyyy-MM-dd format for both from and to"];

    private readonly ISnapshotQueryService _snapshots;
    private readonly ISnapshotStatisticsService _statistics;

    public StatsController(ISnapshotQueryService snapshots, ISnapshotStatisticsService statistics)
    {
        _snapshots = snapshots;
        _statistics = statistics;
    }

    /// <summary>
    /// Get all snapshots (paginated)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<SnapshotResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1)
        {
            return BadRequest(new { error = "Invalid page", details = PageDetails });
        }
        if (pageSize < 1)
        {
            return BadRequest(new { error = "Invalid pageSize", details = PageSizeDetails });
        }

        pageSize = Math.Min(pageSize, SnapshotQueryService.MaxPageSize);

        var (items, totalCount) = await _snapshots.GetAllAsync(page, pageSize);

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
    /// <param name="date">Date in yyyy-MM-dd format</param>
    /// <param name="tz">UTC offset in minutes (e.g. 60 for CET, 120 for CEST)</param>
    [HttpGet("daily/{date}")]
    [ProducesResponseType(typeof(DailyStatsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDaily(string date, [FromQuery] int tz = 0)
    {
        if (!DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return BadRequest(new { error = "Invalid date format", details = DateFormatDetails });
        }

        var snapshots = await _snapshots.GetByDateAsync(parsedDate, tz);
        var summary = await _statistics.GetDailySummaryAsync(parsedDate, tz);

        var snapshotDtos = new List<SnapshotResponseDto>();
        if (snapshots.Count > 0)
        {
            var (startOfDay, _) = LocalDay.BoundsUtc(parsedDate, tz);
            var baseline = await _snapshots.GetLastSnapshotBeforeAsync(startOfDay);
            if (baseline != null)
            {
                snapshotDtos.Add(MapToDto(baseline));
            }
        }
        snapshotDtos.AddRange(snapshots.Select(MapToDto));

        var response = new DailyStatsResponseDto
        {
            Date = date,
            Snapshots = snapshotDtos,
            Summary = summary
        };

        return Ok(response);
    }

    /// <summary>
    /// Get statistics for a date range
    /// </summary>
    /// <param name="from">Start date in yyyy-MM-dd format</param>
    /// <param name="to">End date in yyyy-MM-dd format</param>
    /// <param name="tz">UTC offset in minutes (e.g. 60 for CET, 120 for CEST)</param>
    [HttpGet("range")]
    [ProducesResponseType(typeof(RangeStatsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRange([FromQuery] string from, [FromQuery] string to, [FromQuery] int tz = 0)
    {
        if (!DateOnly.TryParseExact(from, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate)
            || !DateOnly.TryParseExact(to, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
        {
            return BadRequest(new { error = "Invalid date format", details = RangeFormatDetails });
        }

        var response = new RangeStatsResponseDto
        {
            From = from,
            To = to,
            Data = await _statistics.GetRangeAggregateAsync(fromDate, toDate, tz)
        };

        return Ok(response);
    }

    /// <summary>
    /// Get heatmap data (hour x day of week)
    /// </summary>
    /// <param name="weeks">Number of weeks to include (max <see cref="MaxHeatmapWeeks"/>)</param>
    /// <param name="tz">UTC offset in minutes (e.g. 60 for CET, 120 for CEST)</param>
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(HeatmapResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeatmap([FromQuery] int weeks = 4, [FromQuery] int tz = 0)
    {
        weeks = Math.Min(weeks, MaxHeatmapWeeks);

        var heatmapData = await _statistics.GetHeatmapDataAsync(weeks, tz);

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
        var lastSnapshot = await _snapshots.GetLatestAsync();

        var response = new HealthResponseDto
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Database = await _snapshots.IsDatabaseReachableAsync() ? "connected" : "disconnected",
            LastSnapshot = lastSnapshot?.Timestamp
        };

        return Ok(response);
    }

    private static SnapshotResponseDto MapToDto(MachineSnapshot snapshot)
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
