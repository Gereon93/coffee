using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeApi.Controllers;

/// <summary>
/// Controls the power state of the Siemens EQ900 coffee machine via HomeConnect
/// </summary>
[ApiController]
[Route("coffee")]
public class PowerController : ControllerBase
{
    private readonly IHomeConnectService _homeConnect;
    private readonly ILogger<PowerController> _logger;

    public PowerController(IHomeConnectService homeConnect, ILogger<PowerController> logger)
    {
        _homeConnect = homeConnect;
        _logger = logger;
    }

    /// <summary>
    /// Turn the coffee machine on or off
    /// </summary>
    /// <param name="request">{ "state": "on" | "off" }</param>
    [HttpPost("power")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetPower([FromBody] PowerRequestDto request)
    {
        if (request.State is not ("on" or "off"))
        {
            return BadRequest(new { status = "error", message = "state must be 'on' or 'off'" });
        }

        var turnOn = request.State == "on";

        try
        {
            await _homeConnect.SetPowerStateAsync(turnOn);
            return Ok(new { status = "ok", state = request.State });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set power state to {State}", request.State);
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}
