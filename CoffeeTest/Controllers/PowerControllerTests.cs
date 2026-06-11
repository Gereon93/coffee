using CoffeeApi.Controllers;
using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Controllers;

public class PowerControllerTests
{
    /// <summary>Records SetPowerStateAsync calls and can simulate a failing service.</summary>
    private sealed class RecordingHomeConnect : IHomeConnectService
    {
        public int SetPowerCallCount { get; private set; }
        public bool? LastOn { get; private set; }
        public Exception? ThrowOnSetPower { get; set; }

        public Task SetPowerStateAsync(bool on)
        {
            SetPowerCallCount++;
            LastOn = on;
            if (ThrowOnSetPower is not null)
            {
                throw ThrowOnSetPower;
            }

            return Task.CompletedTask;
        }

        public Task<CoffeeStatusDto> GetStatusAsync() => Task.FromResult(new CoffeeStatusDto());
    }

    private static (PowerController controller, RecordingHomeConnect stub) Create()
    {
        var stub = new RecordingHomeConnect();
        return (new PowerController(stub, NullLogger<PowerController>.Instance), stub);
    }

    [Fact]
    public async Task SetPower_StateOn_TurnsMachineOnAndReturnsOk()
    {
        var (controller, stub) = Create();

        var result = await controller.SetPower(new PowerRequestDto { State = "on" });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, stub.SetPowerCallCount);
        Assert.True(stub.LastOn);
    }

    [Fact]
    public async Task SetPower_StateOff_TurnsMachineOffAndReturnsOk()
    {
        var (controller, stub) = Create();

        var result = await controller.SetPower(new PowerRequestDto { State = "off" });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, stub.SetPowerCallCount);
        Assert.False(stub.LastOn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ON")]      // case-sensitive: only lowercase is valid
    [InlineData("toggle")]
    [InlineData("1")]
    public async Task SetPower_InvalidState_Returns400_WithoutCallingService(string state)
    {
        var (controller, stub) = Create();

        var result = await controller.SetPower(new PowerRequestDto { State = state });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, stub.SetPowerCallCount);
    }

    [Fact]
    public async Task SetPower_WhenServiceThrows_Returns500()
    {
        var (controller, stub) = Create();
        stub.ThrowOnSetPower = new HttpRequestException("webhook unreachable");

        var result = await controller.SetPower(new PowerRequestDto { State = "on" });

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, obj.StatusCode);
    }
}
