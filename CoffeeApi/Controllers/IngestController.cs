using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeApi.Controllers;

/// <summary>
/// Controller for ingesting EQ900 snapshots from n8n
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private static readonly string[] PayloadDetails = ["data.status is required"];

    private readonly ISnapshotIngestService _ingestService;
    private readonly ILogger<IngestController> _logger;

    public IngestController(ISnapshotIngestService ingestService, ILogger<IngestController> logger)
    {
        _ingestService = ingestService;
        _logger = logger;
    }

    /// <summary>
    /// Ingest EQ900 snapshot from n8n
    /// </summary>
    /// <param name="payload">Home Connect status payload</param>
    /// <returns>201 if new snapshot created, 200 if duplicate skipped</returns>
    [HttpPost]
    [ProducesResponseType(typeof(IngestResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IngestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest([FromBody] IngestPayloadDto payload)
    {
        if (payload?.Data?.Status == null || payload.Data.Status.Count == 0)
        {
            _logger.LogWarning("Invalid ingest payload: data.status is required");
            return BadRequest(new { error = "Invalid payload", details = PayloadDetails });
        }

        if (!IngestPayloadValidator.TryValidateCupCounters(payload.Data.Status, out var validationDetails))
        {
            _logger.LogWarning("Invalid ingest payload: cup counter validation failed");
            return BadRequest(new { error = "Invalid payload", details = validationDetails });
        }

        try
        {
            var (created, snapshot) = await _ingestService.ProcessIngestAsync(payload);

            var response = new IngestResponseDto
            {
                Id = snapshot.Id,
                Created = created,
                Timestamp = snapshot.Timestamp,
                Message = created
                    ? "Snapshot created"
                    : "No counter change detected, snapshot skipped"
            };

            if (created)
            {
                return Created($"/api/stats/{snapshot.Id}", response);
            }

            return Ok(response);
        }
        catch (InvalidIngestPayloadException ex)
        {
            _logger.LogWarning("Invalid ingest payload: {Details}", ex.Details);
            return BadRequest(new { error = "Invalid payload", details = ex.Details });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ingest payload");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
