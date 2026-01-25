using System.Text.Json.Serialization;

namespace CoffeeApi.DTOs;

/// <summary>
/// Root DTO for n8n payload containing Home Connect status data
/// </summary>
public class IngestPayloadDto
{
    [JsonPropertyName("data")]
    public IngestDataDto Data { get; set; } = new();
}

/// <summary>
/// Container for status items array
/// </summary>
public class IngestDataDto
{
    [JsonPropertyName("status")]
    public List<StatusItemDto> Status { get; set; } = new();
}

/// <summary>
/// Individual status item from Home Connect API
/// </summary>
public class StatusItemDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object Value { get; set; } = null!;

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}

/// <summary>
/// Response DTO for ingest endpoint
/// </summary>
public class IngestResponseDto
{
    public int Id { get; set; }
    public bool Created { get; set; }
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
}
