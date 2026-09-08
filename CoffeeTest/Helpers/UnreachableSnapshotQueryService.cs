using CoffeeApi.Domain;
using CoffeeApi.Services;

namespace CoffeeTest.Helpers;

public sealed class UnreachableSnapshotQueryService : ISnapshotQueryService
{
    private readonly bool _reachable;
    private readonly bool _queryThrows;
    private readonly bool _probeThrows;

    private UnreachableSnapshotQueryService(bool reachable, bool queryThrows, bool probeThrows)
    {
        _reachable = reachable;
        _queryThrows = queryThrows;
        _probeThrows = probeThrows;
    }

    public static UnreachableSnapshotQueryService ProbeReportsDisconnected() =>
        new(reachable: false, queryThrows: true, probeThrows: false);

    public static UnreachableSnapshotQueryService ProbeThrows() =>
        new(reachable: false, queryThrows: true, probeThrows: true);

    public static UnreachableSnapshotQueryService QueryFailsAfterReachableProbe() =>
        new(reachable: true, queryThrows: true, probeThrows: false);

    public bool LatestRequested { get; private set; }

    public Task<bool> IsDatabaseReachableAsync() =>
        _probeThrows
            ? Task.FromException<bool>(new InvalidOperationException("database unreachable"))
            : Task.FromResult(_reachable);

    public Task<MachineSnapshot?> GetLatestAsync(string machineId = ISnapshotQueryService.DefaultMachineId)
    {
        LatestRequested = true;
        return _queryThrows
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
