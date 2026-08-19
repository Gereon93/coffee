using CoffeeApi.Infrastructure;
using CoffeeApi.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Helpers;

/// <summary>
/// Wires the three snapshot services against one test context.
/// </summary>
public static class SnapshotServices
{
    public static SnapshotQueryService Query(AppDbContext context) => new(context);

    public static SnapshotStatisticsService Statistics(AppDbContext context) =>
        new(Query(context), context);

    public static SnapshotIngestService Ingest(AppDbContext context) =>
        new(context, Query(context), NullLogger<SnapshotIngestService>.Instance);
}
