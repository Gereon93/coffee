using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Services;

/// <inheritdoc cref="IBeanHopperService"/>
public class BeanHopperService : IBeanHopperService
{
    private const string CounterDetail = "counter must be 'coffee' or 'coffeeAndMilk'";
    private const string HopperDetail = "beanHopper must be 1, 2 or null";

    private readonly AppDbContext _context;
    private readonly ISnapshotQueryService _snapshots;

    public BeanHopperService(AppDbContext context, ISnapshotQueryService snapshots)
    {
        _context = context;
        _snapshots = snapshots;
    }

    public async Task<Dictionary<int, List<BeanHopperUsageDto>>> GetUsageAsync(IReadOnlyList<MachineSnapshot> sequence)
    {
        var usageBySnapshot = new Dictionary<int, List<BeanHopperUsageDto>>();

        if (sequence.Count < 2)
        {
            return usageBySnapshot;
        }

        var overrides = await LoadOverridesAsync(sequence);

        for (int i = 1; i < sequence.Count; i++)
        {
            var current = sequence[i];
            var usages = new List<BeanHopperUsageDto>();

            foreach (var counter in BeanCounters.All)
            {
                var count = BeanCounters.DeltaOf(counter, sequence[i - 1], current);
                if (count == 0)
                {
                    continue;
                }

                var isManual = overrides.TryGetValue((current.Id, counter), out var stored);

                usages.Add(new BeanHopperUsageDto
                {
                    Counter = counter,
                    Count = count,
                    BeanHopper = isManual ? stored!.BeanHopper : BeanCounters.DefaultHopper(counter),
                    Source = isManual ? BeanHopperSources.Manual : BeanHopperSources.Auto
                });
            }

            if (usages.Count > 0)
            {
                usageBySnapshot[current.Id] = usages;
            }
        }

        return usageBySnapshot;
    }

    public async Task<BeanHopperTotalsDto> GetTotalsAsync(IReadOnlyList<MachineSnapshot> sequence)
    {
        var usageBySnapshot = await GetUsageAsync(sequence);
        return SumUsage(usageBySnapshot.Values.SelectMany(u => u));
    }

    public BeanHopperTotalsDto SumUsage(IEnumerable<BeanHopperUsageDto> usages)
    {
        var totals = new BeanHopperTotalsDto();

        foreach (var usage in usages)
        {
            switch (usage.BeanHopper)
            {
                case 1:
                    totals.Hopper1 += usage.Count;
                    break;
                case 2:
                    totals.Hopper2 += usage.Count;
                    break;
                default:
                    totals.Excluded += usage.Count;
                    break;
            }
        }

        return totals;
    }

    public async Task<(bool Success, BeanHopperError Error, string? Detail)> SetOverrideAsync(int snapshotId, SetBeanHopperDto dto)
    {
        if (!BeanCounters.IsValid(dto.Counter))
        {
            return (false, BeanHopperError.InvalidCounter, CounterDetail);
        }

        if (dto.BeanHopper is not (null or 1 or 2))
        {
            return (false, BeanHopperError.InvalidHopper, HopperDetail);
        }

        var snapshot = await _context.MachineSnapshots.FindAsync(snapshotId);
        if (snapshot == null)
        {
            return (false, BeanHopperError.SnapshotNotFound, $"No snapshot with id {snapshotId}");
        }

        // Reassigning a hopper only means something where drinks were actually
        // drawn. Without that guard a typo in the id would store an inert row.
        var previous = await _snapshots.GetLastSnapshotBeforeAsync(snapshot.Timestamp);
        if (previous == null || BeanCounters.DeltaOf(dto.Counter, previous, snapshot) == 0)
        {
            return (false, BeanHopperError.NoConsumption,
                $"Snapshot {snapshotId} has no {dto.Counter} delta to reassign");
        }

        var existing = await FindOverrideAsync(snapshotId, dto.Counter);
        if (existing != null)
        {
            existing.BeanHopper = dto.BeanHopper;
        }
        else
        {
            _context.BeanHopperOverrides.Add(new BeanHopperOverride
            {
                SnapshotId = snapshotId,
                Counter = dto.Counter,
                BeanHopper = dto.BeanHopper
            });
        }

        await _context.SaveChangesAsync();
        return (true, BeanHopperError.None, null);
    }

    public async Task<(bool Success, BeanHopperError Error, string? Detail)> ClearOverrideAsync(int snapshotId, string counter)
    {
        if (!BeanCounters.IsValid(counter))
        {
            return (false, BeanHopperError.InvalidCounter, CounterDetail);
        }

        var existing = await FindOverrideAsync(snapshotId, counter);
        if (existing == null)
        {
            return (false, BeanHopperError.OverrideNotFound,
                $"Snapshot {snapshotId} has no {counter} override");
        }

        _context.BeanHopperOverrides.Remove(existing);
        await _context.SaveChangesAsync();
        return (true, BeanHopperError.None, null);
    }

    private Task<BeanHopperOverride?> FindOverrideAsync(int snapshotId, string counter)
    {
        return _context.BeanHopperOverrides
            .FirstOrDefaultAsync(o => o.SnapshotId == snapshotId && o.Counter == counter);
    }

    private async Task<Dictionary<(int SnapshotId, string Counter), BeanHopperOverride>> LoadOverridesAsync(
        IReadOnlyList<MachineSnapshot> sequence)
    {
        var ids = sequence.Skip(1).Select(s => s.Id).ToList();

        var stored = await _context.BeanHopperOverrides
            .Where(o => ids.Contains(o.SnapshotId))
            .ToListAsync();

        return stored.ToDictionary(o => (o.SnapshotId, o.Counter));
    }
}
