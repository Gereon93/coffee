using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeApi.Controllers;

[ApiController]
[Route("api/admin/historical-backfill")]
public class HistoricalBackfillController : ControllerBase
{
    private readonly IHistoricalBackfillService _service;

    public HistoricalBackfillController(IHistoricalBackfillService service)
    {
        _service = service;
    }

    [HttpPost("preview")]
    [ProducesResponseType(typeof(HistoricalBackfillPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromBody] HistoricalBackfillRequestDto request)
    {
        var result = await _service.PreviewAsync(request.CommissionedAt);
        return result.Success
            ? Ok(result.Plan)
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("apply")]
    [ProducesResponseType(typeof(HistoricalBackfillPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Apply([FromBody] HistoricalBackfillRequestDto request)
    {
        var result = await _service.ApplyAsync(request.CommissionedAt);
        if (result.Success)
        {
            return Ok(result.Plan);
        }

        return result.Plan?.AlreadyApplied == true
            ? Conflict(new { error = result.Error, plan = result.Plan })
            : BadRequest(new { error = result.Error });
    }
}
