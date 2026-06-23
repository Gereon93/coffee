using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeApi.Controllers;

[ApiController]
[Route("api/stats/marked-days")]
public class MarkedDaysController : ControllerBase
{
    private readonly IMarkedDayService _markedDayService;

    public MarkedDaysController(IMarkedDayService markedDayService)
    {
        _markedDayService = markedDayService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MarkedDayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? kind = null)
    {
        if (kind != null && !_markedDayService.IsValidKind(kind))
        {
            return BadRequest(new { error = "Invalid kind", details = new[] { "kind must be 'mass-import' or 'event'" } });
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
            var details = new[] { detail! };
            return error switch
            {
                MarkedDayError.AlreadyMarked => Conflict(new { error = "Already marked", details }),
                MarkedDayError.InvalidDate => BadRequest(new { error = "Invalid date", details }),
                MarkedDayError.InvalidKind => BadRequest(new { error = "Invalid kind", details }),
                MarkedDayError.InvalidEventType => BadRequest(new { error = "Invalid eventType", details }),
                MarkedDayError.ReasonRequired => BadRequest(new { error = "Reason required", details }),
                _ => BadRequest(new { error = "Unknown error", details })
            };
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
            var details = new[] { detail! };
            return error switch
            {
                MarkedDayError.NotFound => NotFound(new { error = "Not found", details }),
                MarkedDayError.InvalidDate => BadRequest(new { error = "Invalid date", details }),
                _ => BadRequest(new { error = "Unknown error", details })
            };
        }

        return NoContent();
    }
}
