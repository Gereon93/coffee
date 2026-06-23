using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Services;

public class MarkedDayService : IMarkedDayService
{
    private static readonly HashSet<string> ValidKinds = new() { "mass-import", "event" };
    private static readonly HashSet<string> ValidEventTypes = new()
    {
        "birthday", "visitors", "party", "sick", "vacation", "other"
    };

    private readonly AppDbContext _context;
    private readonly ILogger<MarkedDayService> _logger;

    public MarkedDayService(AppDbContext context, ILogger<MarkedDayService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarkedDayDto>> GetAllAsync(string? kind = null)
    {
        var query = _context.MarkedDays.AsQueryable();
        if (kind != null)
        {
            query = query.Where(d => d.Kind == kind);
        }

        return await query
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
    }

    public async Task<(bool Success, MarkedDayDto? Dto, string? Error, string? Detail)> CreateAsync(CreateMarkedDayDto dto)
    {
        if (!DateOnly.TryParseExact(dto.Date, "yyyy-MM-dd", out var parsedDate))
        {
            return (false, null, "Invalid date", "Use yyyy-MM-dd format");
        }

        var kind = string.IsNullOrWhiteSpace(dto.Kind) ? "mass-import" : dto.Kind;
        if (!ValidKinds.Contains(kind))
        {
            return (false, null, "Invalid kind", $"kind must be one of: {string.Join(", ", ValidKinds)}");
        }

        string? eventType = null;
        if (kind == "event")
        {
            if (string.IsNullOrWhiteSpace(dto.EventType) || !ValidEventTypes.Contains(dto.EventType))
            {
                return (false, null, "Invalid eventType", $"eventType must be one of: {string.Join(", ", ValidEventTypes)}");
            }
            eventType = dto.EventType;
        }

        if (kind == "mass-import" && string.IsNullOrWhiteSpace(dto.Reason))
        {
            return (false, null, "Reason required", "Reason must not be empty for mass-import");
        }

        var exists = await _context.MarkedDays.AnyAsync(d => d.Date == parsedDate);
        if (exists)
        {
            return (false, null, "Already marked", $"Day {dto.Date} is already marked");
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

        return (true, response, null, null);
    }

    public async Task<(bool Success, string? Error, string? Detail)> DeleteAsync(string date)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
        {
            return (false, "Invalid date", "Use yyyy-MM-dd format");
        }

        var entity = await _context.MarkedDays.FirstOrDefaultAsync(d => d.Date == parsedDate);
        if (entity == null)
        {
            return (false, "Not found", $"Day {date} is not marked");
        }

        _context.MarkedDays.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Day {Date} unmarked", date);
        return (true, null, null);
    }
}
