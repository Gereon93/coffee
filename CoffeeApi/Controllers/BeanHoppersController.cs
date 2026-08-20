using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeApi.Controllers;

/// <summary>
/// Manual corrections of the bean hopper a snapshot delta was drawn from.
/// </summary>
[ApiController]
[Route("api/stats/snapshots")]
public class BeanHoppersController : ControllerBase
{
    private readonly IBeanHopperService _beanHoppers;

    public BeanHoppersController(IBeanHopperService beanHoppers)
    {
        _beanHoppers = beanHoppers;
    }

    /// <summary>
    /// Correct the hopper of one counter within one snapshot delta
    /// </summary>
    /// <param name="id">Snapshot the delta ends at</param>
    /// <param name="dto">Counter to correct and the hopper it drew from</param>
    [HttpPost("{id:int}/bean-hopper")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetBeanHopper(int id, [FromBody] SetBeanHopperDto dto)
    {
        var (success, error, detail) = await _beanHoppers.SetOverrideAsync(id, dto);

        return success ? NoContent() : ToErrorResult(error, detail);
    }

    /// <summary>
    /// Drop a correction so the delta falls back to the automatic rule
    /// </summary>
    /// <param name="id">Snapshot the delta ends at</param>
    /// <param name="counter">"coffee" or "coffeeAndMilk"</param>
    [HttpDelete("{id:int}/bean-hopper")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearBeanHopper(int id, [FromQuery] string counter)
    {
        var (success, error, detail) = await _beanHoppers.ClearOverrideAsync(id, counter);

        return success ? NoContent() : ToErrorResult(error, detail);
    }

    private IActionResult ToErrorResult(BeanHopperError error, string? detail)
    {
        var details = new[] { detail! };

        return error switch
        {
            BeanHopperError.InvalidCounter => BadRequest(new { error = "Invalid counter", details }),
            BeanHopperError.InvalidHopper => BadRequest(new { error = "Invalid beanHopper", details }),
            BeanHopperError.NoConsumption => BadRequest(new { error = "No bean consumption", details }),
            BeanHopperError.SnapshotNotFound => NotFound(new { error = "Snapshot not found", details }),
            BeanHopperError.OverrideNotFound => NotFound(new { error = "Override not found", details }),
            _ => BadRequest(new { error = "Unknown error", details })
        };
    }
}
