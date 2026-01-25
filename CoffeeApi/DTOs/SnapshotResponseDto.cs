namespace CoffeeApi.DTOs;

/// <summary>
/// Response DTO for single snapshot
/// </summary>
public class SnapshotResponseDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int TotalBeverages { get; set; }
    public int BeverageCounterCoffee { get; set; }
    public int BeverageCounterCoffeeAndMilk { get; set; }
    public int BeverageCounterMilk { get; set; }
    public int BeverageCounterHotWaterCups { get; set; }
    public int BeverageCounterHotWater { get; set; }
    public string OperationState { get; set; } = string.Empty;
}

/// <summary>
/// Paginated response wrapper
/// </summary>
public class PaginatedResponseDto<T>
{
    public List<T> Data { get; set; } = new();
    public PaginationDto Pagination { get; set; } = new();
}

/// <summary>
/// Pagination metadata
/// </summary>
public class PaginationDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Daily statistics response
/// </summary>
public class DailyStatsResponseDto
{
    public string Date { get; set; } = string.Empty;
    public List<SnapshotResponseDto> Snapshots { get; set; } = new();
    public DailySummaryDto Summary { get; set; } = new();
}

/// <summary>
/// Summary for a single day
/// </summary>
public class DailySummaryDto
{
    public int CoffeeToday { get; set; }
    public int MilkDrinksToday { get; set; }
    public int TotalToday { get; set; }
    public int? PeakHour { get; set; }
}

/// <summary>
/// Range query response
/// </summary>
public class RangeStatsResponseDto
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public List<DailyAggregateDto> Data { get; set; } = new();
}

/// <summary>
/// Aggregated daily data for range queries
/// </summary>
public class DailyAggregateDto
{
    public string Date { get; set; } = string.Empty;
    public int CoffeeCount { get; set; }
    public int MilkCount { get; set; }
    public int Total { get; set; }
}

/// <summary>
/// Heatmap response
/// </summary>
public class HeatmapResponseDto
{
    public int Weeks { get; set; }
    public List<HeatmapDataPointDto> Heatmap { get; set; } = new();
}

/// <summary>
/// Single heatmap data point (hour x day of week)
/// </summary>
public class HeatmapDataPointDto
{
    public int DayOfWeek { get; set; } // ISO-8601: 1=Monday, 7=Sunday
    public int Hour { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Health check response
/// </summary>
public class HealthResponseDto
{
    public string Status { get; set; } = "healthy";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Database { get; set; } = "connected";
    public DateTime? LastSnapshot { get; set; }
}
