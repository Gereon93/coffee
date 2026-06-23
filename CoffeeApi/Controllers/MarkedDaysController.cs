using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeApi.Controllers;

[ApiController]
[Route("api/stats/marked-days")]
public class MarkedDaysController : ControllerBase
{
    private static readonly HashSet<string> ValidKinds = new() { "mass-import", "event" };

    private readonly IMarkedDayService _markedDayService;

    public MarkedDaysController(IMarkedDayService markedDayService)
    {
        _markedDayService = markedDayService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MarkedDayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? kind = null)
    {
        if (kind != null && !ValidKinds.Contains(kind))
        {
            return BadRequest(new { error = "Invalid kind", details = new[] { $"kind must be one of: {string.Join(", ", ValidKinds)}" } });
        }

        var days = await _markedDayService.GetAllAsync(kind);
        return Ok(days);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MarkedDayDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateMarkedDayDto dto)
    {
        var (success, markedDayDto, error, detail) = await _markedDayService.CreateAsync(dto);

        if (!success)
        {
            if (error == "Already marked")
            {
                return Conflict(new { error, details = new[] { detail! } });
            }
            return BadRequest(new { error, details = new[] { detail! } });
        }

        return CreatedAtAction(nameof(GetAll), markedDayDto);
    }

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string date)
    {
        var (success, error, detail) = await _markedDayService.DeleteAsync(date);

        if (!success)
        {
            if (error == "Not found")
            {
                return NotFound(new { error, details = new[] { detail! } });
            }
            return BadRequest(new { error, details = new[] { detail! } });
        }

        return NoContent();
    }
}
