namespace CoffeeApi.Services;

public sealed class InvalidIngestPayloadException(IReadOnlyList<string> details)
    : Exception("Invalid ingest payload")
{
    public IReadOnlyList<string> Details { get; } = details;
}
