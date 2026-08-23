using CoffeeApi.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoffeeTest.Infrastructure;

public class EstimatedSnapshotFlagMigrationTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"estimated-backfill-{Guid.NewGuid():N}.db");

    private AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options);
    }

    [Fact]
    public async Task MigrationAddsFlagWithoutChangingExistingSnapshots()
    {
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync("20260820224917_AddBeanHopperOverrides");
            await SeedFirstRealSnapshotAsync(context);
            await context.Database.MigrateAsync();

            var snapshot = await context.MachineSnapshots.SingleAsync();
            Assert.False(snapshot.IsEstimated);
            Assert.Equal(988, snapshot.BeverageCounterCoffee);
            Assert.Equal(10, snapshot.BeverageCounterCoffeeAndMilk);
            Assert.Equal(11, snapshot.BeverageCounterMilk);
            Assert.Equal(1, snapshot.BeverageCounterHotWaterCups);
        }

        await using (var context = CreateContext())
        {
            await context.GetService<IMigrator>()
                .MigrateAsync("20260820224917_AddBeanHopperOverrides");

            var remaining = await CountRowsAsync(context, "MachineSnapshots");
            Assert.Equal(1, remaining);

            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('MachineSnapshots') WHERE name = 'IsEstimated'";
            if (command.Connection!.State != System.Data.ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }

            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static async Task SeedFirstRealSnapshotAsync(AppDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "MachineSnapshots" (
                "Timestamp", "MachineId", "BeverageCounterCoffee", "BeverageCounterCoffeeAndMilk",
                "BeverageCounterMilk", "BeverageCounterHotWaterCups", "BeverageCounterHotWater",
                "OperationState", "RemoteControlAllowed", "LocalControlActive",
                "InteriorIlluminationActive", "CreatedAt"
            ) VALUES (
                '2026-01-25 10:15:00', 'EQ900-DEFAULT', 988, 10, 11, 1, 150,
                'Ready', 0, 0, 0, '2026-01-25 10:15:00'
            );
            """);
    }

    private static async Task<long> CountRowsAsync(AppDbContext context, string table)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync();
        }

        return (long)(await command.ExecuteScalarAsync())!;
    }

}
