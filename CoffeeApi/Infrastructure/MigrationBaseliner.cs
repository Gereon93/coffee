using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoffeeApi.Infrastructure;

/// <summary>
/// Auto-baseline an existing pre-migration SQLite database so
/// <see cref="DatabaseFacade.Migrate"/> does not try to re-create
/// tables that EnsureCreated() already produced.
/// Safe to call on every startup — idempotent.
/// </summary>
public static class MigrationBaseliner
{
    public static void EnsureBaselined(AppDbContext context, ILogger logger)
    {
        var conn = context.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) conn.Open();

        try
        {
            if (!HasTable(conn, "MachineSnapshots"))
            {
                logger.LogDebug("Baseliner: no MachineSnapshots table — fresh DB, skipping");
                return;
            }

            if (HasTable(conn, "__EFMigrationsHistory"))
            {
                logger.LogDebug("Baseliner: __EFMigrationsHistory already present — already baselined");
                return;
            }

            var initialMigrationId = context.Database
                .GetService<IMigrationsAssembly>()
                .Migrations
                .Select(m => m.Key)
                .OrderBy(id => id)
                .FirstOrDefault();

            if (initialMigrationId == null)
            {
                logger.LogWarning("Baseliner: no migrations registered — cannot baseline");
                return;
            }

            logger.LogInformation(
                "Baseliner: detected pre-migration DB, seeding __EFMigrationsHistory with {MigrationId}",
                initialMigrationId);

            using var tx = conn.BeginTransaction();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES (@id, '9.0.0');
                """;
                var param = cmd.CreateParameter();
                param.ParameterName = "@id";
                param.Value = initialMigrationId;
                cmd.Parameters.Add(param);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    private static bool HasTable(System.Data.Common.DbConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name LIMIT 1";
        var param = cmd.CreateParameter();
        param.ParameterName = "@name";
        param.Value = tableName;
        cmd.Parameters.Add(param);
        return cmd.ExecuteScalar() != null;
    }
}
