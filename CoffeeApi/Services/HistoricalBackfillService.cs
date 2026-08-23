using System.Data;
using System.Globalization;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Services;

public class HistoricalBackfillService : IHistoricalBackfillService
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int SnapshotHourUtc = 12;
    private static readonly SemaphoreSlim ApplyGate = new(1, 1);

    private readonly AppDbContext _context;
    private readonly ILogger<HistoricalBackfillService> _logger;

    public HistoricalBackfillService(AppDbContext context, ILogger<HistoricalBackfillService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool Success, HistoricalBackfillPlanDto? Plan, string? Error)> PreviewAsync(
        string commissionedAt,
        string machineId = "EQ900-DEFAULT")
    {
        var preparation = await PrepareAsync(commissionedAt, machineId);
        if (!preparation.Success)
        {
            return (false, null, preparation.Error);
        }

        return (true, BuildPlan(preparation), null);
    }

    public async Task<(bool Success, HistoricalBackfillPlanDto? Plan, string? Error)> ApplyAsync(
        string commissionedAt,
        string machineId = "EQ900-DEFAULT")
    {
        await ApplyGate.WaitAsync();
        try
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
                : null;

            var preparation = await PrepareAsync(commissionedAt, machineId);
            if (!preparation.Success)
            {
                return (false, null, preparation.Error);
            }

            var plan = BuildPlan(preparation);
            if (plan.AlreadyApplied)
            {
                return (false, plan, "Historical backfill has already been applied.");
            }

            var target = preparation.FirstSnapshot;
            var days = plan.EndDate.DayNumber - preparation.CommissionedAt.DayNumber;
            var snapshots = new List<MachineSnapshot>(plan.EstimatedSnapshotCount);

            snapshots.Add(CreateEstimatedSnapshot(preparation.CommissionedAt, target, 0, days));
            for (var day = 1; day <= days; day++)
            {
                snapshots.Add(CreateEstimatedSnapshot(
                    preparation.CommissionedAt.AddDays(day), target, day, days));
            }

            _context.MachineSnapshots.AddRange(snapshots);
            await _context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            _logger.LogInformation(
                "Created {Count} estimated historical snapshots through {EndDate} from first real snapshot {SnapshotId}",
                snapshots.Count,
                plan.EndDate,
                target.Id);

            return (true, plan, null);
        }
        finally
        {
            ApplyGate.Release();
        }
    }

    private async Task<Preparation> PrepareAsync(string commissionedAt, string machineId)
    {
        if (string.IsNullOrWhiteSpace(machineId))
        {
            return Preparation.Failed("machineId must not be empty.");
        }

        if (!DateOnly.TryParseExact(
                commissionedAt,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var commissionedDate))
        {
            return Preparation.Failed("commissionedAt must use yyyy-MM-dd format.");
        }

        var firstSnapshot = await _context.MachineSnapshots
            .Where(snapshot => !snapshot.IsEstimated && snapshot.MachineId == machineId)
            .OrderBy(snapshot => snapshot.Timestamp)
            .ThenBy(snapshot => snapshot.Id)
            .FirstOrDefaultAsync();

        if (firstSnapshot == null)
        {
            return Preparation.Failed("No real snapshot exists yet.");
        }

        var endDate = new DateOnly(firstSnapshot.Timestamp.Year - 1, 12, 31);
        if (commissionedDate >= endDate)
        {
            return Preparation.Failed("commissionedAt must be before the historical target period.");
        }

        var alreadyApplied = await _context.MachineSnapshots
            .AnyAsync(snapshot => snapshot.IsEstimated && snapshot.MachineId == machineId);
        return new Preparation(machineId, commissionedDate, endDate, firstSnapshot, alreadyApplied, null);
    }

    private static HistoricalBackfillPlanDto BuildPlan(Preparation preparation)
    {
        var estimatedSnapshotCount = preparation.EndDate.DayNumber - preparation.CommissionedAt.DayNumber + 1;
        return new HistoricalBackfillPlanDto
        {
            MachineId = preparation.MachineId,
            CommissionedAt = preparation.CommissionedAt.ToString(DateFormat, CultureInfo.InvariantCulture),
            EndDate = preparation.EndDate,
            FirstSnapshotId = preparation.FirstSnapshot.Id,
            FirstSnapshotTimestamp = preparation.FirstSnapshot.Timestamp,
            EstimatedSnapshotCount = estimatedSnapshotCount,
            TargetBeverageCount = preparation.FirstSnapshot.BeverageCounterCoffee
                + preparation.FirstSnapshot.BeverageCounterCoffeeAndMilk
                + preparation.FirstSnapshot.BeverageCounterMilk,
            TargetTotalBeverages = preparation.FirstSnapshot.TotalBeverages,
            AlreadyApplied = preparation.AlreadyApplied
        };
    }

    private static MachineSnapshot CreateEstimatedSnapshot(
        DateOnly date,
        MachineSnapshot target,
        int day,
        int totalDays)
    {
        return new MachineSnapshot
        {
            Timestamp = new DateTime(date.Year, date.Month, date.Day, SnapshotHourUtc, 0, 0, DateTimeKind.Utc),
            MachineId = target.MachineId,
            BeverageCounterCoffee = Interpolate(target.BeverageCounterCoffee, day, totalDays),
            BeverageCounterCoffeeAndMilk = Interpolate(target.BeverageCounterCoffeeAndMilk, day, totalDays),
            BeverageCounterMilk = Interpolate(target.BeverageCounterMilk, day, totalDays),
            BeverageCounterHotWaterCups = Interpolate(target.BeverageCounterHotWaterCups, day, totalDays),
            BeverageCounterHotWater = Interpolate(target.BeverageCounterHotWater, day, totalDays),
            OperationState = "Estimated",
            IsEstimated = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static int Interpolate(int target, int day, int totalDays)
    {
        return (int)((long)target * day / totalDays);
    }

    private sealed record Preparation(
        string MachineId,
        DateOnly CommissionedAt,
        DateOnly EndDate,
        MachineSnapshot FirstSnapshot,
        bool AlreadyApplied,
        string? Error)
    {
        public bool Success => Error == null;

        public static Preparation Failed(string error) => new(string.Empty, default, default, null!, false, error);
    }
}
