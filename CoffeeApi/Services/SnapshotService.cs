using System.Text.Json;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Services;

/// <summary>
/// Service for snapshot operations with idempotency logic
/// </summary>
public class SnapshotService : ISnapshotService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SnapshotService> _logger;

    // Home Connect key mappings
    private static readonly Dictionary<string, string> KeyMappings = new()
    {
        ["BSH.Common.Status.OperationState"] = "OperationState",
        ["BSH.Common.Status.RemoteControlStartAllowed"] = "RemoteControlAllowed",
        ["BSH.Common.Status.LocalControlActive"] = "LocalControlActive",
        ["BSH.Common.Status.InteriorIlluminationActive"] = "InteriorIlluminationActive",
        ["ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee"] = "BeverageCounterCoffee",
        ["ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk"] = "BeverageCounterCoffeeAndMilk",
        ["ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk"] = "BeverageCounterMilk",
        ["ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups"] = "BeverageCounterHotWaterCups",
        ["ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater"] = "BeverageCounterHotWater"
    };

    public SnapshotService(AppDbContext context, ILogger<SnapshotService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool Created, MachineSnapshot Snapshot)> ProcessIngestAsync(IngestPayloadDto payload)
    {
        var newSnapshot = MapToEntity(payload);

        // 1. Load latest snapshot
        var lastSnapshot = await GetLatestAsync(newSnapshot.MachineId);

        // 2. Idempotency check: Only save if counters increased
        if (lastSnapshot != null && !HasCounterIncreased(lastSnapshot, newSnapshot))
        {
            _logger.LogDebug("Snapshot skipped - no counter increase detected");
            return (false, lastSnapshot);
        }

        // 3. Save new snapshot
        _context.MachineSnapshots.Add(newSnapshot);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New snapshot created: {Id}, Coffee: {Coffee}, Total: {Total}",
            newSnapshot.Id, newSnapshot.BeverageCounterCoffee, newSnapshot.TotalBeverages);

        return (true, newSnapshot);
    }

    public async Task<MachineSnapshot?> GetLatestAsync(string machineId = "EQ900-DEFAULT")
    {
        return await _context.MachineSnapshots
            .Where(s => s.MachineId == machineId)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<MachineSnapshot> Items, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 50)
    {
        pageSize = Math.Min(pageSize, 100); // Cap at 100
        var totalCount = await _context.MachineSnapshots.CountAsync();

        var items = await _context.MachineSnapshots
            .OrderByDescending(s => s.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<MachineSnapshot>> GetByDateAsync(DateOnly date, int tzOffsetMinutes = 0)
    {
        var (start, end) = GetLocalDayBoundsUtc(date, tzOffsetMinutes);

        return await _context.MachineSnapshots
            .Where(s => s.Timestamp >= start && s.Timestamp < end)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();
    }

    public async Task<List<MachineSnapshot>> GetByDateRangeAsync(DateOnly from, DateOnly to, int tzOffsetMinutes = 0)
    {
        var (start, _) = GetLocalDayBoundsUtc(from, tzOffsetMinutes);
        var (_, end) = GetLocalDayBoundsUtc(to, tzOffsetMinutes);

        return await _context.MachineSnapshots
            .Where(s => s.Timestamp >= start && s.Timestamp < end)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();
    }

    public async Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date, int tzOffsetMinutes = 0)
    {
        var snapshots = await GetByDateAsync(date, tzOffsetMinutes);

        if (snapshots.Count == 0)
        {
            return new DailySummaryDto();
        }

        // Get the last snapshot before this day for cross-day delta
        var (startOfDay, _) = GetLocalDayBoundsUtc(date, tzOffsetMinutes);
        var previousSnapshot = await _context.MachineSnapshots
            .Where(s => s.Timestamp < startOfDay)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();

        var baseline = previousSnapshot ?? snapshots.First();
        var last = snapshots.Last();

        // Calculate today's consumption (delta from baseline to last)
        var coffeeToday = last.BeverageCounterCoffee - baseline.BeverageCounterCoffee;
        var milkDrinksToday = (last.BeverageCounterCoffeeAndMilk - baseline.BeverageCounterCoffeeAndMilk) +
                             (last.BeverageCounterMilk - baseline.BeverageCounterMilk);

        // Find peak hour (hour with most beverages)
        int? peakHour = null;
        var maxDelta = 0;

        // Build sequence including previous day's last snapshot for first delta
        var sequence = previousSnapshot != null
            ? new[] { previousSnapshot }.Concat(snapshots).ToList()
            : snapshots;

        for (int i = 1; i < sequence.Count; i++)
        {
            var prev = sequence[i - 1];
            var curr = sequence[i];
            var delta = curr.TotalBeverages - prev.TotalBeverages;

            if (delta > maxDelta)
            {
                maxDelta = delta;
                // Return peak hour in caller's local time
                peakHour = curr.Timestamp.AddMinutes(tzOffsetMinutes).Hour;
            }
        }

        return new DailySummaryDto
        {
            CoffeeToday = Math.Max(0, coffeeToday),
            MilkDrinksToday = Math.Max(0, milkDrinksToday),
            TotalToday = Math.Max(0, coffeeToday + milkDrinksToday),
            PeakHour = peakHour
        };
    }

    public async Task<List<HeatmapDataPointDto>> GetHeatmapDataAsync(int weeks = 4, int tzOffsetMinutes = 0)
    {
        var startDate = DateTime.UtcNow.AddDays(-7 * weeks);

        var snapshots = await _context.MachineSnapshots
            .Where(s => s.Timestamp >= startDate)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();

        var excludedDates = (await _context.MarkedDays
            .Where(d => d.Kind == "mass-import")
            .Select(d => d.Date)
            .ToListAsync()).ToHashSet();

        // Group by day of week and hour, count consumption deltas
        var heatmapData = new Dictionary<(int DayOfWeek, int Hour), int>();

        for (int i = 1; i < snapshots.Count; i++)
        {
            var prev = snapshots[i - 1];
            var curr = snapshots[i];
            var delta = curr.TotalBeverages - prev.TotalBeverages;

            if (delta > 0)
            {
                // Convert to caller's local time for grouping
                var localTime = curr.Timestamp.AddMinutes(tzOffsetMinutes);

                // Skip deltas landing on mass-import / excluded local dates
                if (excludedDates.Contains(DateOnly.FromDateTime(localTime)))
                    continue;

                // ISO-8601: Monday = 1, Sunday = 7
                var dayOfWeek = (int)localTime.DayOfWeek;
                if (dayOfWeek == 0) dayOfWeek = 7; // Convert Sunday from 0 to 7

                var key = (dayOfWeek, localTime.Hour);

                if (!heatmapData.ContainsKey(key))
                    heatmapData[key] = 0;

                heatmapData[key] += delta;
            }
        }

        return heatmapData
            .Select(kvp => new HeatmapDataPointDto
            {
                DayOfWeek = kvp.Key.DayOfWeek,
                Hour = kvp.Key.Hour,
                Count = kvp.Value
            })
            .OrderBy(h => h.DayOfWeek)
            .ThenBy(h => h.Hour)
            .ToList();
    }

    /// <summary>
    /// Calculates the UTC start (inclusive) and end (exclusive) of a local day.
    /// </summary>
    private static (DateTime Start, DateTime End) GetLocalDayBoundsUtc(DateOnly date, int tzOffsetMinutes)
    {
        // Local midnight in UTC = midnight - offset
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            .AddMinutes(-tzOffsetMinutes);
        var end = start.AddDays(1);
        return (start, end);
    }

    private MachineSnapshot MapToEntity(IngestPayloadDto payload)
    {
        var snapshot = new MachineSnapshot
        {
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in payload.Data.Status)
        {
            switch (item.Key)
            {
                case "BSH.Common.Status.OperationState":
                    snapshot.OperationState = ExtractOperationState(item.Value?.ToString() ?? "");
                    break;
                case "BSH.Common.Status.RemoteControlStartAllowed":
                    snapshot.RemoteControlAllowed = ConvertToBool(item.Value);
                    break;
                case "BSH.Common.Status.LocalControlActive":
                    snapshot.LocalControlActive = ConvertToBool(item.Value);
                    break;
                case "BSH.Common.Status.InteriorIlluminationActive":
                    snapshot.InteriorIlluminationActive = ConvertToBool(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee":
                    snapshot.BeverageCounterCoffee = ConvertToInt(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk":
                    snapshot.BeverageCounterCoffeeAndMilk = ConvertToInt(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk":
                    snapshot.BeverageCounterMilk = ConvertToInt(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups":
                    snapshot.BeverageCounterHotWaterCups = ConvertToInt(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater":
                    snapshot.BeverageCounterHotWater = ConvertToInt(item.Value);
                    break;
            }
        }

        return snapshot;
    }

    private static string ExtractOperationState(string fullValue)
    {
        // Extract "Ready" from "BSH.Common.EnumType.OperationState.Ready"
        var parts = fullValue.Split('.');
        return parts.Length > 0 ? parts[^1] : "Unknown";
    }

    private static bool ConvertToBool(object? value)
    {
        return value switch
        {
            bool b => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            string s => bool.TryParse(s, out var result) && result,
            _ => false
        };
    }

    private static int ConvertToInt(object? value)
    {
        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            string s => int.TryParse(s, out var result) ? result : 0,
            _ => 0
        };
    }

    private static bool HasCounterIncreased(MachineSnapshot last, MachineSnapshot current)
    {
        return current.BeverageCounterCoffee > last.BeverageCounterCoffee
            || current.BeverageCounterCoffeeAndMilk > last.BeverageCounterCoffeeAndMilk
            || current.BeverageCounterMilk > last.BeverageCounterMilk
            || current.BeverageCounterHotWaterCups > last.BeverageCounterHotWaterCups;
    }
}
