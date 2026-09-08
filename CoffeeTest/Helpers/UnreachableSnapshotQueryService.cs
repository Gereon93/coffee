using CoffeeApi.Domain;
using CoffeeApi.Services;

namespace CoffeeTest.Helpers;

/// <summary>
/// Simulates a database in various broken states: probe returns dead/throws,
/// or the probe succeeds and the subsequent query fails.
/// </summary>
public sealed class UnreachableSnapshotQueryService(
    bool reachable = false,
    bool queryThrows = true,
    bool probeThrows = false) : ISnapshotQueryService
{
    public bool LatestRequested { get; private set; }

    public Task<bool> IsDatabaseReachableAsync() =>
        probeThrows
            ? Task.FromException<bool>(new InvalidOperationException("database unreachable"))
            : Task.FromResult(reachable);

    public Task<MachineSnapshot?> GetLatestAsync(string machineId = ISnapshotQueryService.DefaultMachineId)
    {
        LatestRequested = true;
        return queryThrows
            ? Task.FromException<MachineSnapshot?>(new InvalidOperationException("database unreachable"))
            : Task.FromResult<MachineSnapshot?>(null);
    }

    public Task<(List<MachineSnapshot> Items, int TotalCount)> GetAllAsync(
        int page = 1, int pageSize = 50, string machineId = ISnapshotQueryService.DefaultMachineId) =>
        throw new NotSupportedException();

    public Task<List<MachineSnapshot>> GetByDateAsync(
        DateOnly date, int tzOffsetMinutes = 0, string machineId = ISnapshotQueryService.DefaultMachineId) =>
        throw new NotSupportedException();

    public Task<List<MachineSnapshot>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, int tzOffsetMinutes = 0, string machineId = ISnapshotQueryService.DefaultMachineId) =>
        throw new NotSupportedException();

    public Task<List<MachineSnapshot>> GetSinceAsync(
        DateTime fromUtc, string machineId = ISnapshotQueryService.DefaultMachineId) =>
        throw new NotSupportedException();

    public Task<MachineSnapshot?> GetLastSnapshotBeforeAsync(
        DateTime timestampUtc, string machineId = ISnapshotQueryService.DefaultMachineId) =>
        throw new NotSupportedException();
}
