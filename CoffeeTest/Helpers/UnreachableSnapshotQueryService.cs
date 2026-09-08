using CoffeeApi.Domain;
using CoffeeApi.Services;

namespace CoffeeTest.Helpers;

/// <summary>
/// Simulates an unreachable database: the probe either reports the connection
/// as dead or throws, and any query fails.
/// </summary>
public sealed class UnreachableSnapshotQueryService(bool probeThrows) : ISnapshotQueryService
{
    public bool LatestRequested { get; private set; }

    public Task<bool> IsDatabaseReachableAsync() =>
        probeThrows
            ? Task.FromException<bool>(new InvalidOperationException("database unreachable"))
            : Task.FromResult(false);

    public Task<MachineSnapshot?> GetLatestAsync(string machineId = ISnapshotQueryService.DefaultMachineId)
    {
        LatestRequested = true;
        return Task.FromException<MachineSnapshot?>(new InvalidOperationException("database unreachable"));
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
