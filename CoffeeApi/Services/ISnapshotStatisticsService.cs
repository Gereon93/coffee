using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

/// <summary>
/// Turns stored snapshots into consumption figures. Counters are cumulative, so
/// every number here is a delta against a baseline, never a raw reading.
/// </summary>
public interface ISnapshotStatisticsService
{
    /// <summary>
    /// Get daily statistics summary for a local date
    /// </summary>
    Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date, int tzOffsetMinutes = 0);

    /// <summary>
    /// Aggregate consumption per local day across an inclusive date range
    /// </summary>
    Task<List<DailyAggregateDto>> GetRangeAggregateAsync(DateOnly from, DateOnly to, int tzOffsetMinutes = 0);

    /// <summary>
    /// Get aggregated data for the weekday-by-hour heatmap
    /// </summary>
    Task<List<HeatmapDataPointDto>> GetHeatmapDataAsync(int weeks = 4, int tzOffsetMinutes = 0);
}
