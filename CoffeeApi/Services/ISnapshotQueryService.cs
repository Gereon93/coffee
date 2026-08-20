using CoffeeApi.Domain;

namespace CoffeeApi.Services;

/// <summary>
/// Reads stored snapshots. No aggregation, no ingest — every method answers
/// "which rows?" and nothing else. Every ordered result breaks ties on equal
/// timestamps by id, so two callers walking the same rows see the same order.
/// </summary>
public interface ISnapshotQueryService
{
    /// <summary>
    /// Get the latest snapshot for a machine
    /// </summary>
    Task<MachineSnapshot?> GetLatestAsync(string machineId = "EQ900-DEFAULT");

    /// <summary>
    /// Get all snapshots with pagination
    /// </summary>
    Task<(List<MachineSnapshot> Items, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 50);

    /// <summary>
    /// Get snapshots for a specific local date
    /// </summary>
    /// <param name="date">The local date</param>
    /// <param name="tzOffsetMinutes">UTC offset in minutes (e.g. 60 for CET)</param>
    Task<List<MachineSnapshot>> GetByDateAsync(DateOnly date, int tzOffsetMinutes = 0);

    /// <summary>
    /// Get snapshots within a local date range, both bounds inclusive
    /// </summary>
    Task<List<MachineSnapshot>> GetByDateRangeAsync(DateOnly from, DateOnly to, int tzOffsetMinutes = 0);

    /// <summary>
    /// Get all snapshots taken at or after a UTC timestamp, oldest first
    /// </summary>
    Task<List<MachineSnapshot>> GetSinceAsync(DateTime fromUtc);

    /// <summary>
    /// Get the last snapshot before a given UTC timestamp
    /// </summary>
    Task<MachineSnapshot?> GetLastSnapshotBeforeAsync(DateTime timestampUtc);

    /// <summary>
    /// Probe whether the database is reachable
    /// </summary>
    Task<bool> IsDatabaseReachableAsync();
}
