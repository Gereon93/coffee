using CoffeeApi.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Infrastructure;

public class MigrationBaselinerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public MigrationBaselinerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"baseliner-test-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void EnsureBaselined_PreMigrationDb_InsertsHistoryRow()
    {
        // Arrange: existing MachineSnapshots table but no history
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE "MachineSnapshots" (
                    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                    "Timestamp" TEXT NOT NULL,
                    "MachineId" TEXT NOT NULL DEFAULT 'EQ900-DEFAULT',
                    "OperationState" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
            """;
            cmd.ExecuteNonQuery();
        }

        // Act
        using (var ctx = CreateContext())
        {
            MigrationBaseliner.EnsureBaselined(ctx, NullLogger.Instance);
        }

        // Assert
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
            var count = (long)cmd.ExecuteScalar()!;
            Assert.Equal(1L, count);

            cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory LIMIT 1";
            var id = (string)cmd.ExecuteScalar()!;
            Assert.EndsWith("_Initial", id);
        }
    }

    [Fact]
    public void EnsureBaselined_FreshDb_DoesNothing()
    {
        // Arrange: empty file, no tables
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
        }

        // Act
        using (var ctx = CreateContext())
        {
            MigrationBaseliner.EnsureBaselined(ctx, NullLogger.Instance);
        }

        // Assert: no history table created (that's Migrate's job)
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
            Assert.Null(cmd.ExecuteScalar());
        }
    }

    [Fact]
    public void EnsureBaselined_AlreadyBaselined_Idempotent()
    {
        // Arrange: schema + existing history row
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE "MachineSnapshots" (
                    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                    "Timestamp" TEXT NOT NULL,
                    "MachineId" TEXT NOT NULL,
                    "OperationState" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                INSERT INTO "__EFMigrationsHistory" VALUES ('20260101000000_Initial', '9.0.0');
            """;
            cmd.ExecuteNonQuery();
        }

        // Act
        using (var ctx = CreateContext())
        {
            MigrationBaseliner.EnsureBaselined(ctx, NullLogger.Instance);
        }

        // Assert: no second row added
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
            Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
        }
    }
}
