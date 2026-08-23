using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public interface IHistoricalBackfillService
{
    /// <summary>Builds a historical backfill plan without changing the database.</summary>
    /// <param name="commissionedAt">Installation date in yyyy-MM-dd format.</param>
    /// <param name="machineId">Machine to plan; defaults to EQ900-DEFAULT.</param>
    Task<(bool Success, HistoricalBackfillPlanDto? Plan, string? Error)> PreviewAsync(
        string commissionedAt,
        string machineId = HistoricalBackfillDefaults.MachineId);

    /// <summary>Applies a historical backfill plan to the selected machine.</summary>
    /// <param name="commissionedAt">Installation date in yyyy-MM-dd format.</param>
    /// <param name="machineId">Machine to update; defaults to EQ900-DEFAULT.</param>
    Task<(bool Success, HistoricalBackfillPlanDto? Plan, string? Error)> ApplyAsync(
        string commissionedAt,
        string machineId = HistoricalBackfillDefaults.MachineId);
}
