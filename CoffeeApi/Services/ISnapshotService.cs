using CoffeeApi.Domain;
using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

/// <summary>
/// Service interface for snapshot operations
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Process incoming ingest payload with idempotency check
    /// </summary>
    /// <returns>Tuple of (Created: true if new snapshot, Snapshot: the snapshot entity)</returns>
    Task<(bool Created, MachineSnapshot Snapshot)> ProcessIngestAsync(IngestPayloadDto payload);

    /// <summary>
    /// Get the latest snapshot for a machine
    /// </summary>
    Task<MachineSnapshot?> GetLatestAsync(string machineId = "EQ900-DEFAULT");

    /// <summary>
    /// Get all snapshots with pagination
    /// </summary>
    Task<(List<MachineSnapshot> Items, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 50);

    /// <summary>
    /// Get snapshots for a specific date (timezone-aware)
    /// </summary>
    /// <param name="date">The local date</param>
    /// <param name="tzOffsetMinutes">UTC offset in minutes (e.g. 60 for CET)</param>
    Task<List<MachineSnapshot>> GetByDateAsync(DateOnly date, int tzOffsetMinutes = 0);

    /// <summary>
    /// Get snapshots within a date range (timezone-aware)
    /// </summary>
    Task<List<MachineSnapshot>> GetByDateRangeAsync(DateOnly from, DateOnly to, int tzOffsetMinutes = 0);

    /// <summary>
    /// Get daily statistics summary (timezone-aware)
    /// </summary>
    Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date, int tzOffsetMinutes = 0);

    /// <summary>
    /// Get aggregated data for heatmap (timezone-aware)
    /// </summary>
    Task<List<HeatmapDataPointDto>> GetHeatmapDataAsync(int weeks = 4, int tzOffsetMinutes = 0);
}
