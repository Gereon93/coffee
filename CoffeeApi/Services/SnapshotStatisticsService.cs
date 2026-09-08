using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Services;

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

    public async Task<DailySummaryDto> GetDailySummaryAsync(
        DateOnly date, int tzOffsetMinutes = 0, string machineId = ISnapshotQueryService.DefaultMachineId)
    {
        var snapshots = await _snapshots.GetByDateAsync(date, tzOffsetMinutes, machineId);

        if (snapshots.Count == 0)
        {
            return new DailySummaryDto();
        }

        var (startOfDay, _) = LocalDay.BoundsUtc(date, tzOffsetMinutes);
        var previousSnapshot = await _snapshots.GetLastSnapshotBeforeAsync(startOfDay, machineId);

        var sequence = PrecededBy(previousSnapshot, snapshots);

        var (coffeeToday, milkDrinksToday, _) = BeverageDeltas(sequence);

        return new DailySummaryDto
        {
            CoffeeToday = Math.Max(0, coffeeToday),
            MilkDrinksToday = Math.Max(0, milkDrinksToday),
            TotalToday = Math.Max(0, coffeeToday + milkDrinksToday),
            PeakHour = FindPeakHour(sequence, tzOffsetMinutes),
            BeanHoppers = await _beanHoppers.GetTotalsAsync(sequence)
        };
    }

    public async Task<List<DailyAggregateDto>> GetRangeAggregateAsync(
        DateOnly from, DateOnly to, int tzOffsetMinutes = 0, string machineId = ISnapshotQueryService.DefaultMachineId)
    {
        var snapshots = await _snapshots.GetByDateRangeAsync(from, to, tzOffsetMinutes, machineId);

        var (rangeStart, _) = LocalDay.BoundsUtc(from, tzOffsetMinutes);
        var baseline = await _snapshots.GetLastSnapshotBeforeAsync(rangeStart, machineId);

        var usageBySnapshot = await _beanHoppers.GetUsageAsync(PrecededBy(baseline, snapshots));

        var days = snapshots
            .GroupBy(s => LocalDay.DateOf(s.Timestamp, tzOffsetMinutes))
            .OrderBy(group => group.Key)
            .Select(group => (LocalDate: group.Key, Snapshots: group.OrderBy(s => s.Timestamp).ThenBy(s => s.Id).ToList()));

        var aggregates = new List<DailyAggregateDto>();

        foreach (var (localDate, daySnapshots) in days)
        {
            var daySequence = PrecededBy(baseline, daySnapshots);
            var last = daySnapshots[^1];

            var (coffee, milk, hotWaterCups) = BeverageDeltas(daySequence);

            var dayUsages = daySnapshots
                .Where(s => usageBySnapshot.ContainsKey(s.Id))
                .SelectMany(s => usageBySnapshot[s.Id]);

            aggregates.Add(new DailyAggregateDto
            {
                Date = localDate.ToString("yyyy-MM-dd"),
                CoffeeCount = Math.Max(0, coffee),
                MilkCount = Math.Max(0, milk),
                Total = Math.Max(0, coffee + milk + hotWaterCups),
                BeanHoppers = _beanHoppers.SumUsage(dayUsages)
            });

            baseline = last;
        }

        return aggregates;
    }

    public async Task<List<HeatmapDataPointDto>> GetHeatmapDataAsync(
        int weeks = 4, int tzOffsetMinutes = 0, string machineId = ISnapshotQueryService.DefaultMachineId)
    {
        var snapshots = await _snapshots.GetSinceAsync(
            DateTime.UtcNow.AddDays(-DaysPerWeek * weeks), machineId);
        var excludedDates = await GetMassImportDatesAsync();

        var buckets = new Dictionary<(int DayOfWeek, int Hour), int>();

        for (int i = 1; i < snapshots.Count; i++)
        {
            if (snapshots[i - 1].IsEstimated || snapshots[i].IsEstimated)
            {
                continue;
            }

            var delta = PositiveTotalBeverageDelta(snapshots[i], snapshots[i - 1]);
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

    private static (int Coffee, int MilkDrinks, int HotWaterCups) BeverageDeltas(IReadOnlyList<MachineSnapshot> sequence)
    {
        var coffee = 0;
        var coffeeAndMilk = 0;
        var milk = 0;
        var hotWaterCups = 0;

        for (int i = 1; i < sequence.Count; i++)
        {
            coffee += PositiveCupCounterDelta(sequence[i].BeverageCounterCoffee, sequence[i - 1].BeverageCounterCoffee);
            coffeeAndMilk += PositiveCupCounterDelta(sequence[i].BeverageCounterCoffeeAndMilk, sequence[i - 1].BeverageCounterCoffeeAndMilk);
            milk += PositiveCupCounterDelta(sequence[i].BeverageCounterMilk, sequence[i - 1].BeverageCounterMilk);
            hotWaterCups += PositiveCupCounterDelta(sequence[i].BeverageCounterHotWaterCups, sequence[i - 1].BeverageCounterHotWaterCups);
        }

        return (coffee, coffeeAndMilk + milk, hotWaterCups);
    }

    private static int PositiveCupCounterDelta(int current, int previous) =>
        Math.Max(0, current - previous);

    private static int PositiveTotalBeverageDelta(MachineSnapshot current, MachineSnapshot previous) =>
        PositiveCupCounterDelta(current.BeverageCounterCoffee, previous.BeverageCounterCoffee)
        + PositiveCupCounterDelta(current.BeverageCounterCoffeeAndMilk, previous.BeverageCounterCoffeeAndMilk)
        + PositiveCupCounterDelta(current.BeverageCounterMilk, previous.BeverageCounterMilk)
        + PositiveCupCounterDelta(current.BeverageCounterHotWaterCups, previous.BeverageCounterHotWaterCups);

    private static int? FindPeakHour(List<MachineSnapshot> sequence, int tzOffsetMinutes)
    {
        int? peakHour = null;
        var maxDelta = 0;

        for (int i = 1; i < sequence.Count; i++)
        {
            if (sequence[i - 1].IsEstimated || sequence[i].IsEstimated)
            {
                continue;
            }

            var delta = PositiveTotalBeverageDelta(sequence[i], sequence[i - 1]);

            if (delta > maxDelta)
            {
                maxDelta = delta;
                peakHour = LocalDay.ToLocal(sequence[i].Timestamp, tzOffsetMinutes).Hour;
            }
        }

        return peakHour;
    }

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
