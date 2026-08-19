using CoffeeApi.Domain;
using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

/// <summary>
/// Accepts Home Connect payloads and persists them, applying the counter-based
/// idempotency rule from ADR-005.
/// </summary>
public interface ISnapshotIngestService
{
    /// <summary>
    /// Process incoming ingest payload with idempotency check
    /// </summary>
    /// <returns>Tuple of (Created: true if new snapshot, Snapshot: the snapshot entity)</returns>
    Task<(bool Created, MachineSnapshot Snapshot)> ProcessIngestAsync(IngestPayloadDto payload);
}
