using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;

namespace CoffeeApi.Services;

/// <inheritdoc cref="ISnapshotIngestService"/>
public partial class SnapshotIngestService : ISnapshotIngestService
{
    private readonly AppDbContext _context;
    private readonly ISnapshotQueryService _snapshots;
    private readonly ILogger<SnapshotIngestService> _logger;

    public SnapshotIngestService(
        AppDbContext context,
        ISnapshotQueryService snapshots,
        ILogger<SnapshotIngestService> logger)
    {
        _context = context;
        _snapshots = snapshots;
        _logger = logger;
    }

    public async Task<(bool Created, MachineSnapshot Snapshot)> ProcessIngestAsync(IngestPayloadDto payload)
    {
        var newSnapshot = SnapshotPayloadMapper.Map(payload, DateTime.UtcNow);

        var lastSnapshot = await _snapshots.GetLatestAsync(newSnapshot.MachineId);

        if (lastSnapshot != null && !HasCounterIncreased(lastSnapshot, newSnapshot))
        {
            _logger.LogDebug("Snapshot skipped - no counter increase detected");
            return (false, lastSnapshot);
        }

        _context.MachineSnapshots.Add(newSnapshot);
        await _context.SaveChangesAsync();

        LogSnapshotCreated(newSnapshot.Id, newSnapshot.BeverageCounterCoffee, newSnapshot.TotalBeverages);

        return (true, newSnapshot);
    }

    private static bool HasCounterIncreased(MachineSnapshot last, MachineSnapshot current)
    {
        return current.BeverageCounterCoffee > last.BeverageCounterCoffee
            || current.BeverageCounterCoffeeAndMilk > last.BeverageCounterCoffeeAndMilk
            || current.BeverageCounterMilk > last.BeverageCounterMilk
            || current.BeverageCounterHotWaterCups > last.BeverageCounterHotWaterCups;
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "New snapshot created: {Id}, Coffee: {Coffee}, Total: {Total}")]
    private partial void LogSnapshotCreated(int id, int coffee, int total);
}
