using CoffeeApi.Controllers;
using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeTest.Controllers;

public class CoffeeStatusControllerTests
{
    private sealed class StubHomeConnect : IHomeConnectService
    {
        public int GetStatusCallCount { get; private set; }
        public Func<CoffeeStatusDto>? StatusFactory { get; set; }

        public Task SetPowerStateAsync(bool on) => Task.CompletedTask;

        public Task<CoffeeStatusDto> GetStatusAsync()
        {
            GetStatusCallCount++;
            return Task.FromResult(StatusFactory?.Invoke() ?? new CoffeeStatusDto
            {
                Status = "ok", Reachable = true, PowerState = "on",
                OperationState = "ready", Label = "Bereit", LastUpdated = DateTime.UtcNow
            });
        }
    }

    private static (CoffeeStatusController c, StubHomeConnect stub) CreateController()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var stub = new StubHomeConnect();
        return (new CoffeeStatusController(stub, cache), stub);
    }

    [Fact]
    public async Task GetStatus_ReturnsServicePayload()
    {
        var (controller, _) = CreateController();

        var result = await controller.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CoffeeStatusDto>(ok.Value);
        Assert.True(dto.Reachable);
        Assert.Equal("on", dto.PowerState);
    }

    [Fact]
    public async Task GetStatus_CachesWithinTtl()
    {
        var (controller, stub) = CreateController();

        await controller.GetStatus();
        await controller.GetStatus();
        await controller.GetStatus();

        Assert.Equal(1, stub.GetStatusCallCount);
    }

    [Fact]
    public async Task GetStatus_PassesThroughUnreachableState()
    {
        var (controller, stub) = CreateController();
        stub.StatusFactory = () => new CoffeeStatusDto
        {
            Status = "ok",
            Reachable = false,
            Label = "Offline",
            Message = "Status-Service nicht erreichbar"
        };

        var result = await controller.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CoffeeStatusDto>(ok.Value);
        Assert.False(dto.Reachable);
        Assert.Equal("Offline", dto.Label);
        Assert.Equal("Status-Service nicht erreichbar", dto.Message);
    }
}
