using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public interface IMarkedDayService
{
    Task<IReadOnlyList<MarkedDayDto>> GetAllAsync(string? kind = null);
    Task<(bool Success, MarkedDayDto? Dto, string? Error, string? Detail)> CreateAsync(CreateMarkedDayDto dto);
    Task<(bool Success, string? Error, string? Detail)> DeleteAsync(string date);
}
