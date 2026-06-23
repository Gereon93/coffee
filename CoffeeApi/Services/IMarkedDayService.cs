using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public enum MarkedDayError
{
    None,
    InvalidDate,
    InvalidKind,
    InvalidEventType,
    ReasonRequired,
    AlreadyMarked,
    NotFound
}

public interface IMarkedDayService
{
    Task<IReadOnlyList<MarkedDayDto>> GetAllAsync(string? kind = null);
    Task<(bool Success, MarkedDayDto? Dto, MarkedDayError Error, string? Detail)> CreateAsync(CreateMarkedDayDto dto);
    Task<(bool Success, MarkedDayError Error, string? Detail)> DeleteAsync(string date);
    bool IsValidKind(string? kind);
}
