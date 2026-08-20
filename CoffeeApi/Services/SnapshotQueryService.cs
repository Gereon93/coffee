using CoffeeApi.Domain;
using CoffeeApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Services;

/// <inheritdoc cref="ISnapshotQueryService"/>
public class SnapshotQueryService : ISnapshotQueryService
{
    /// <summary>Upper bound on <c>pageSize</c>; larger requests are clamped.</summary>
    public const int MaxPageSize = 100;

    private readonly AppDbContext _context;

    public SnapshotQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MachineSnapshot?> GetLatestAsync(string machineId = "EQ900-DEFAULT")
    {
        return await _context.MachineSnapshots
            .Where(s => s.MachineId == machineId)
            .OrderByDescending(s => s.Timestamp)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<MachineSnapshot> Items, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 50)
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        var totalCount = await _context.MachineSnapshots.CountAsync();

        var items = await _context.MachineSnapshots
            .OrderByDescending(s => s.Timestamp)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<MachineSnapshot>> GetByDateAsync(DateOnly date, int tzOffsetMinutes = 0)
    {
        var (start, end) = LocalDay.BoundsUtc(date, tzOffsetMinutes);
        return await GetBetweenAsync(start, end);
    }

    public async Task<List<MachineSnapshot>> GetByDateRangeAsync(DateOnly from, DateOnly to, int tzOffsetMinutes = 0)
    {
        var (start, _) = LocalDay.BoundsUtc(from, tzOffsetMinutes);
        var (_, end) = LocalDay.BoundsUtc(to, tzOffsetMinutes);
        return await GetBetweenAsync(start, end);
    }

    public async Task<List<MachineSnapshot>> GetSinceAsync(DateTime fromUtc)
    {
        return await _context.MachineSnapshots
            .Where(s => s.Timestamp >= fromUtc)
            .OrderBy(s => s.Timestamp)
            .ThenBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<MachineSnapshot?> GetLastSnapshotBeforeAsync(DateTime timestampUtc)
    {
        return await _context.MachineSnapshots
            .Where(s => s.Timestamp < timestampUtc)
            .OrderByDescending(s => s.Timestamp)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync();
    }

    public Task<bool> IsDatabaseReachableAsync()
    {
        return _context.Database.CanConnectAsync();
    }

    private async Task<List<MachineSnapshot>> GetBetweenAsync(DateTime startInclusiveUtc, DateTime endExclusiveUtc)
    {
        return await _context.MachineSnapshots
            .Where(s => s.Timestamp >= startInclusiveUtc && s.Timestamp < endExclusiveUtc)
            .OrderBy(s => s.Timestamp)
            .ThenBy(s => s.Id)
            .ToListAsync();
    }
}
