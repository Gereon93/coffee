using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Controllers;

[ApiController]
[Route("api/stats/excluded-days")]
public class ExcludedDaysController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ExcludedDaysController> _logger;

    public ExcludedDaysController(AppDbContext context, ILogger<ExcludedDaysController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExcludedDayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var days = await _context.ExcludedDays
            .OrderByDescending(d => d.Date)
            .Select(d => new ExcludedDayDto
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                Reason = d.Reason,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return Ok(days);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExcludedDayDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateExcludedDayDto dto)
    {
        if (!DateOnly.TryParseExact(dto.Date, "yyyy-MM-dd", out var parsedDate))
        {
            return BadRequest(new { error = "Invalid date", details = new[] { "Use yyyy-MM-dd format" } });
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new { error = "Reason required", details = new[] { "Reason must not be empty" } });
        }

        var exists = await _context.ExcludedDays.AnyAsync(d => d.Date == parsedDate);
        if (exists)
        {
            return Conflict(new { error = "Already excluded", details = new[] { $"Day {dto.Date} is already marked as excluded" } });
        }

        var entity = new ExcludedDay
        {
            Date = parsedDate,
            Reason = dto.Reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.ExcludedDays.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Day {Date} marked as excluded: {Reason}", dto.Date, entity.Reason);

        var response = new ExcludedDayDto
        {
            Date = entity.Date.ToString("yyyy-MM-dd"),
            Reason = entity.Reason,
            CreatedAt = entity.CreatedAt
        };

        return CreatedAtAction(nameof(GetAll), response);
    }

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string date)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
        {
            return BadRequest(new { error = "Invalid date", details = new[] { "Use yyyy-MM-dd format" } });
        }

        var entity = await _context.ExcludedDays.FirstOrDefaultAsync(d => d.Date == parsedDate);
        if (entity == null)
        {
            return NotFound(new { error = "Not found", details = new[] { $"Day {date} is not marked as excluded" } });
        }

        _context.ExcludedDays.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Day {Date} removed from excluded list", date);
        return NoContent();
    }
}
