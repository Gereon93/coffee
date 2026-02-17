namespace CoffeeApi.Services;

public interface IHomeConnectService
{
    Task SetPowerStateAsync(bool on);
}
