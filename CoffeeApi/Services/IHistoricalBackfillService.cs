using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public interface IHistoricalBackfillService
{
    Task<(bool Success, HistoricalBackfillPlanDto? Plan, string? Error)> PreviewAsync(
        string commissionedAt,
        string machineId = "EQ900-DEFAULT");
    Task<(bool Success, HistoricalBackfillPlanDto? Plan, string? Error)> ApplyAsync(
        string commissionedAt,
        string machineId = "EQ900-DEFAULT");
}
