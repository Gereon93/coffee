using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Controllers;

[ApiController]
[Route("api/stats/marked-days")]
public class MarkedDaysController : ControllerBase
{
    private static readonly HashSet<string> ValidKinds = new() { "mass-import", "event" };
    private static readonly HashSet<string> ValidEventTypes = new()
    {
        "birthday", "visitors", "party", "sick", "vacation", "other"
    };

    private readonly AppDbContext _context;
    private readonly ILogger<MarkedDaysController> _logger;

    public MarkedDaysController(AppDbContext context, ILogger<MarkedDaysController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MarkedDayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? kind = null)
    {
        if (kind != null && !ValidKinds.Contains(kind))
        {
            return BadRequest(new { error = "Invalid kind", details = new[] { $"kind must be one of: {string.Join(", ", ValidKinds)}" } });
        }

        var query = _context.MarkedDays.AsQueryable();
        if (kind != null)
        {
            query = query.Where(d => d.Kind == kind);
        }

        var days = await query
            .OrderByDescending(d => d.Date)
            .Select(d => new MarkedDayDto
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                Kind = d.Kind,
                EventType = d.EventType,
                Reason = d.Reason,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return Ok(days);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MarkedDayDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateMarkedDayDto dto)
    {
        if (!DateOnly.TryParseExact(dto.Date, "yyyy-MM-dd", out var parsedDate))
        {
            return BadRequest(new { error = "Invalid date", details = new[] { "Use yyyy-MM-dd format" } });
        }

        var kind = string.IsNullOrWhiteSpace(dto.Kind) ? "mass-import" : dto.Kind;
        if (!ValidKinds.Contains(kind))
        {
            return BadRequest(new { error = "Invalid kind", details = new[] { $"kind must be one of: {string.Join(", ", ValidKinds)}" } });
        }

        string? eventType = null;
        if (kind == "event")
        {
            if (string.IsNullOrWhiteSpace(dto.EventType) || !ValidEventTypes.Contains(dto.EventType))
            {
                return BadRequest(new { error = "Invalid eventType", details = new[] { $"eventType must be one of: {string.Join(", ", ValidEventTypes)}" } });
            }
            eventType = dto.EventType;
        }

        // mass-import: reason is required (existing behaviour)
        // event:       reason is optional (eventType already carries semantics)
        if (kind == "mass-import" && string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new { error = "Reason required", details = new[] { "Reason must not be empty for mass-import" } });
        }

        var exists = await _context.MarkedDays.AnyAsync(d => d.Date == parsedDate);
        if (exists)
        {
            return Conflict(new { error = "Already marked", details = new[] { $"Day {dto.Date} is already marked" } });
        }

        var entity = new MarkedDay
        {
            Date = parsedDate,
            Kind = kind,
            EventType = eventType,
            Reason = (dto.Reason ?? string.Empty).Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.MarkedDays.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Day {Date} marked as {Kind}{EventType}: {Reason}",
            dto.Date, kind, eventType != null ? $"/{eventType}" : "", entity.Reason);

        var response = new MarkedDayDto
        {
            Date = entity.Date.ToString("yyyy-MM-dd"),
            Kind = entity.Kind,
            EventType = entity.EventType,
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

        var entity = await _context.MarkedDays.FirstOrDefaultAsync(d => d.Date == parsedDate);
        if (entity == null)
        {
            return NotFound(new { error = "Not found", details = new[] { $"Day {date} is not marked" } });
        }

        _context.MarkedDays.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Day {Date} unmarked", date);
        return NoContent();
    }
}
