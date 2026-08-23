using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public interface IHistoricalBackfillService
{
    Task<(bool Success, HistoricalBackfillPlanDto? Plan, string? Error)> PreviewAsync(string commissionedAt);
    Task<(bool Success, HistoricalBackfillPlanDto? Plan, string? Error)> ApplyAsync(string commissionedAt);
}
