using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Services;

/// <inheritdoc cref="ISnapshotStatisticsService"/>
public class SnapshotStatisticsService : ISnapshotStatisticsService
{
    private const string MassImportKind = "mass-import";
    private const int DaysPerWeek = 7;

    private readonly ISnapshotQueryService _snapshots;
    private readonly IBeanHopperService _beanHoppers;
    private readonly AppDbContext _context;

    public SnapshotStatisticsService(
        ISnapshotQueryService snapshots,
        IBeanHopperService beanHoppers,
        AppDbContext context)
    {
        _snapshots = snapshots;
        _beanHoppers = beanHoppers;
        _context = context;
    }

    public async Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date, int tzOffsetMinutes = 0)
    {
        var snapshots = await _snapshots.GetByDateAsync(date, tzOffsetMinutes);

        if (snapshots.Count == 0)
        {
            return new DailySummaryDto();
        }

        var (startOfDay, _) = LocalDay.BoundsUtc(date, tzOffsetMinutes);
        var previousSnapshot = await _snapshots.GetLastSnapshotBeforeAsync(startOfDay);

        var baseline = previousSnapshot ?? snapshots[0];
        var last = snapshots[^1];

        var (coffeeToday, milkDrinksToday) = BeverageDeltas(baseline, last);

        var sequence = PrecededBy(previousSnapshot, snapshots);

        return new DailySummaryDto
        {
            CoffeeToday = Math.Max(0, coffeeToday),
            MilkDrinksToday = Math.Max(0, milkDrinksToday),
            TotalToday = Math.Max(0, coffeeToday + milkDrinksToday),
            PeakHour = FindPeakHour(sequence, tzOffsetMinutes),
            BeanHoppers = await _beanHoppers.GetTotalsAsync(sequence)
        };
    }

    public async Task<List<DailyAggregateDto>> GetRangeAggregateAsync(DateOnly from, DateOnly to, int tzOffsetMinutes = 0)
    {
        var snapshots = await _snapshots.GetByDateRangeAsync(from, to, tzOffsetMinutes);

        var (rangeStart, _) = LocalDay.BoundsUtc(from, tzOffsetMinutes);
        var baseline = await _snapshots.GetLastSnapshotBeforeAsync(rangeStart);

        var usageBySnapshot = await _beanHoppers.GetUsageAsync(PrecededBy(baseline, snapshots));

        var days = snapshots
            .GroupBy(s => LocalDay.DateOf(s.Timestamp, tzOffsetMinutes))
            .OrderBy(group => group.Key)
            .Select(group => (LocalDate: group.Key, Snapshots: group.OrderBy(s => s.Timestamp).ToList()));

        var aggregates = new List<DailyAggregateDto>();

        foreach (var (localDate, daySnapshots) in days)
        {
            var dayBaseline = baseline ?? daySnapshots[0];
            var last = daySnapshots[^1];

            var (coffee, milk) = BeverageDeltas(dayBaseline, last);

            var dayUsages = daySnapshots
                .Where(s => usageBySnapshot.ContainsKey(s.Id))
                .SelectMany(s => usageBySnapshot[s.Id]);

            aggregates.Add(new DailyAggregateDto
            {
                Date = localDate.ToString("yyyy-MM-dd"),
                CoffeeCount = Math.Max(0, coffee),
                MilkCount = Math.Max(0, milk),
                Total = Math.Max(0, last.TotalBeverages - dayBaseline.TotalBeverages),
                BeanHoppers = _beanHoppers.SumUsage(dayUsages)
            });

            baseline = last;
        }

        return aggregates;
    }

    public async Task<List<HeatmapDataPointDto>> GetHeatmapDataAsync(int weeks = 4, int tzOffsetMinutes = 0)
    {
        var snapshots = await _snapshots.GetSinceAsync(DateTime.UtcNow.AddDays(-DaysPerWeek * weeks));
        var excludedDates = await GetMassImportDatesAsync();

        var buckets = new Dictionary<(int DayOfWeek, int Hour), int>();

        for (int i = 1; i < snapshots.Count; i++)
        {
            var delta = snapshots[i].TotalBeverages - snapshots[i - 1].TotalBeverages;
            if (delta <= 0)
            {
                continue;
            }

            var localTime = LocalDay.ToLocal(snapshots[i].Timestamp, tzOffsetMinutes);
            if (excludedDates.Contains(DateOnly.FromDateTime(localTime)))
            {
                continue;
            }

            var bucket = (IsoDayOfWeek(localTime), localTime.Hour);
            buckets[bucket] = buckets.GetValueOrDefault(bucket) + delta;
        }

        return buckets
            .Select(entry => new HeatmapDataPointDto
            {
                DayOfWeek = entry.Key.DayOfWeek,
                Hour = entry.Key.Hour,
                Count = entry.Value
            })
            .OrderBy(h => h.DayOfWeek)
            .ThenBy(h => h.Hour)
            .ToList();
    }

    private static List<MachineSnapshot> PrecededBy(MachineSnapshot? earlierReading, List<MachineSnapshot> snapshots)
    {
        return earlierReading != null
            ? new[] { earlierReading }.Concat(snapshots).ToList()
            : snapshots;
    }

    /// <summary>
    /// Coffee and milk-drink deltas between two cumulative readings.
    /// </summary>
    private static (int Coffee, int MilkDrinks) BeverageDeltas(MachineSnapshot baseline, MachineSnapshot last)
    {
        var coffee = last.BeverageCounterCoffee - baseline.BeverageCounterCoffee;
        var milkDrinks = (last.BeverageCounterCoffeeAndMilk - baseline.BeverageCounterCoffeeAndMilk) +
                         (last.BeverageCounterMilk - baseline.BeverageCounterMilk);
        return (coffee, milkDrinks);
    }

    /// <summary>
    /// The local hour carrying the largest single delta, or <c>null</c> if nothing was brewed.
    /// </summary>
    private static int? FindPeakHour(List<MachineSnapshot> sequence, int tzOffsetMinutes)
    {
        int? peakHour = null;
        var maxDelta = 0;

        for (int i = 1; i < sequence.Count; i++)
        {
            var delta = sequence[i].TotalBeverages - sequence[i - 1].TotalBeverages;

            if (delta > maxDelta)
            {
                maxDelta = delta;
                peakHour = LocalDay.ToLocal(sequence[i].Timestamp, tzOffsetMinutes).Hour;
            }
        }

        return peakHour;
    }

    /// <summary>ISO-8601 weekday numbering: Monday = 1 … Sunday = 7.</summary>
    private static int IsoDayOfWeek(DateTime localTime)
    {
        var dayOfWeek = (int)localTime.DayOfWeek;
        return dayOfWeek == 0 ? DaysPerWeek : dayOfWeek;
    }

    private async Task<HashSet<DateOnly>> GetMassImportDatesAsync()
    {
        var dates = await _context.MarkedDays
            .Where(d => d.Kind == MassImportKind)
            .Select(d => d.Date)
            .ToListAsync();

        return dates.ToHashSet();
    }
}
