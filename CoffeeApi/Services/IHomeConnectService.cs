using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public interface IHomeConnectService
{
    Task SetPowerStateAsync(bool on);
    Task<CoffeeStatusDto> GetStatusAsync();
}
