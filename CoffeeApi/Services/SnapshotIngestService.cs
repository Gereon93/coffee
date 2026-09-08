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
        if (!IngestPayloadValidator.TryValidateCupCounters(payload.Data.Status, out var details))
        {
            throw new InvalidIngestPayloadException(details);
        }

        var newSnapshot = SnapshotPayloadMapper.Map(payload, DateTime.UtcNow);

        var lastSnapshot = await _snapshots.GetLatestAsync(newSnapshot.MachineId);

        if (lastSnapshot != null && !ShouldPersistReading(lastSnapshot, newSnapshot))
        {
            _logger.LogDebug("Snapshot skipped - no counter change detected");
            return (false, lastSnapshot);
        }

        _context.MachineSnapshots.Add(newSnapshot);
        await _context.SaveChangesAsync();

        LogSnapshotCreated(newSnapshot.Id, newSnapshot.BeverageCounterCoffee, newSnapshot.TotalBeverages);

        return (true, newSnapshot);
    }

    private static bool ShouldPersistReading(MachineSnapshot last, MachineSnapshot current) =>
        HasCounterIncrease(last, current) || HasCounterReset(last, current);

    private static bool HasCounterIncrease(MachineSnapshot last, MachineSnapshot current) =>
        current.BeverageCounterCoffee > last.BeverageCounterCoffee
        || current.BeverageCounterCoffeeAndMilk > last.BeverageCounterCoffeeAndMilk
        || current.BeverageCounterMilk > last.BeverageCounterMilk
        || current.BeverageCounterHotWaterCups > last.BeverageCounterHotWaterCups;

    private static bool HasCounterReset(MachineSnapshot last, MachineSnapshot current) =>
        current.BeverageCounterCoffee < last.BeverageCounterCoffee
        || current.BeverageCounterCoffeeAndMilk < last.BeverageCounterCoffeeAndMilk
        || current.BeverageCounterMilk < last.BeverageCounterMilk
        || current.BeverageCounterHotWaterCups < last.BeverageCounterHotWaterCups;

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "New snapshot created: {Id}, Coffee: {Coffee}, Total: {Total}")]
    private partial void LogSnapshotCreated(int id, int coffee, int total);
}
