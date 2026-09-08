namespace CoffeeApi.Services;

public sealed class InvalidIngestPayloadException : Exception
{
    public InvalidIngestPayloadException(IReadOnlyList<string> details)
        : base("Invalid ingest payload")
    {
        Details = details;
    }

    public IReadOnlyList<string> Details { get; }
}
