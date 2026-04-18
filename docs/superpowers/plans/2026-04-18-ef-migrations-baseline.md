# EF Migrations + Auto-Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Von `db.Database.EnsureCreated()` auf EF Core Migrations umstellen, ohne die Prod-DB zu zerstören und **ohne manuellen SSH/SQL-Schritt auf der NAS**. Deployment wird ein reines `docker pull + restart`.

**Architecture:** Eine initiale Migration beschreibt das aktuelle Schema. Ein neuer Helper `MigrationBaseliner` läuft **beim API-Start** vor `Migrate()`: Wenn die DB bereits `MachineSnapshots` enthält, aber keine `__EFMigrationsHistory`, dann fügt er einen History-Eintrag für die Initial-Migration ein — EF überspringt sie dann beim Migrate und lässt existierende Daten unberührt. Bei einer frischen DB macht der Baseliner nichts und `Migrate()` legt alles sauber an. Der Helper ist idempotent: läuft bei jedem Start, aber agiert nur wenn nötig.

**Tech Stack:** EF Core 9.0.0 + SQLite, `dotnet ef` CLI, xUnit für Baseliner-Tests.

---

## File Structure

| File | Responsibility |
|------|---------------|
| `CoffeeApi/Migrations/<timestamp>_Initial.cs` | Auto-gen: CREATE TABLE für `MachineSnapshots` + Indexe |
| `CoffeeApi/Migrations/AppDbContextModelSnapshot.cs` | Auto-gen: Schema-Snapshot |
| `CoffeeApi/Infrastructure/MigrationBaseliner.cs` | Neu: Detectet Pre-Migration-DB und seedet History-Tabelle |
| `CoffeeApi/Program.cs:46-51` | `EnsureCreated()` → `MigrationBaseliner.EnsureBaselined() + Migrate()` |
| `CoffeeTest/Infrastructure/MigrationBaselinerTests.cs` | Unit-Tests für alle Baseliner-Szenarien (SQLite-File, nicht InMemory) |
| `README.md` | Neuer Abschnitt "Schema-Migrationen" |
| `PROJECT_STATE.md` | Aenderungshistorie-Eintrag |

**Tests-Hinweis:** `MigrationBaseliner` arbeitet mit raw SQLite-Commands, also brauchen seine Tests einen echten temporären SQLite-File (kein InMemory, weil InMemory keine sqlite_master-Introspection unterstützt). Bestehende Tests in `CoffeeTest/Helpers/TestDbContextFactory.cs` bleiben unverändert (InMemory mit `EnsureCreated()`).

---

## Prerequisites

- [ ] **Branch:** Wir sind bereits auf `feat/ef-and-massimport` (einem branch mit Sonarqube-Removal als erstem Commit).

- [ ] **dotnet-ef Tool installieren** (falls noch nicht):
  ```bash
  dotnet tool install --global dotnet-ef
  dotnet tool update --global dotnet-ef
  dotnet ef --version
  ```
  Expected: 9.x.x.

---

## Task 1: Lokales Schema-Snapshot (zur Verifikation)

**Files:**
- Create: `scripts/current-schema.sql` (temporär, wird in Task 3 gelöscht)

- [ ] **Step 1: API mit leerer DB starten und Schema ziehen**

```bash
cd CoffeeApi
rm -f coffee.db
dotnet run &
SERVER_PID=$!
sleep 5
curl -s http://localhost:5000/api/health > /dev/null
kill $SERVER_PID
cd ..
mkdir -p scripts
sqlite3 CoffeeApi/coffee.db .schema > scripts/current-schema.sql
cat scripts/current-schema.sql
```

Expected: CREATE TABLE `MachineSnapshots` + 3 Indexe (`IX_MachineSnapshots_Timestamp`, `IX_MachineSnapshots_MachineId`, `IX_MachineSnapshots_Idempotency`).

---

## Task 2: Initiale Migration generieren

**Files:**
- Create: `CoffeeApi/Migrations/<timestamp>_Initial.cs` (+ Designer, ModelSnapshot)

- [ ] **Step 1: Migration generieren**

```bash
cd CoffeeApi
dotnet ef migrations add Initial
cd ..
ls CoffeeApi/Migrations/
```

Expected: drei neue Dateien. Merke dir den `<timestamp>_Initial` Namen.

- [ ] **Step 2: SQL-Script zum Vergleich generieren**

```bash
cd CoffeeApi
dotnet ef migrations script --output ../scripts/migration-initial.sql
cd ..
```

- [ ] **Step 3: Commit**

```bash
git add CoffeeApi/Migrations/
git commit -m "chore(db): add EF initial migration matching current schema"
```

---

## Task 3: Migration gegen Live-Schema verifizieren

- [ ] **Step 1: Vergleichen**

```bash
diff <(grep -A100 'CREATE TABLE "MachineSnapshots"' scripts/current-schema.sql) \
     <(grep -A100 'CREATE TABLE "MachineSnapshots"' scripts/migration-initial.sql)
```

**Must match:** Spaltendefinitionen (Name, Typ, Nullability, Default), Primary Key, alle Indexe.

**Darf abweichen:** `migration-initial.sql` hat zusätzlich `__EFMigrationsHistory` Create+Insert, `current-schema.sql` nicht. Reihenfolge der Spalten-Definitionen kann verdreht sein.

- [ ] **Step 2: Bei Abweichung — OnModelCreating fixen**

```bash
cd CoffeeApi
dotnet ef migrations remove
# In AppDbContext.cs OnModelCreating anpassen
dotnet ef migrations add Initial
cd ..
```
Wiederhole Step 1.

- [ ] **Step 3: Aufräumen**

```bash
rm scripts/current-schema.sql scripts/migration-initial.sql
rm -f CoffeeApi/coffee.db
```

Scripts-Ordner ggf. löschen wenn leer:
```bash
rmdir scripts 2>/dev/null || true
```

---

## Task 4: `MigrationBaseliner` mit TDD schreiben

**Files:**
- Create: `CoffeeApi/Infrastructure/MigrationBaseliner.cs`
- Create: `CoffeeTest/Infrastructure/MigrationBaselinerTests.cs`
- Modify: `CoffeeTest/CoffeeTest.csproj` (add `Microsoft.EntityFrameworkCore.Sqlite` if not present)

- [ ] **Step 1: .csproj für SQLite-Test-Abhängigkeit prüfen**

```bash
grep Sqlite CoffeeTest/CoffeeTest.csproj || echo "missing"
```

Wenn `missing`: `Microsoft.EntityFrameworkCore.Sqlite` zu `CoffeeTest/CoffeeTest.csproj` hinzufügen (gleiche Version wie in `CoffeeApi.csproj`: `9.0.0`).

- [ ] **Step 2: Test für Szenario "Pre-Migration-DB" schreiben**

Erstelle `CoffeeTest/Infrastructure/MigrationBaselinerTests.cs`:

```csharp
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
        // Arrange: Simuliere eine DB aus der EnsureCreated()-Ära
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
            MigrationBaseliner.EnsureBaselined(ctx, NullLogger<MigrationBaseliner>.Instance);
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
        // Arrange: komplett leere DB ohne Tabellen
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
        }

        // Act
        using (var ctx = CreateContext())
        {
            MigrationBaseliner.EnsureBaselined(ctx, NullLogger<MigrationBaseliner>.Instance);
        }

        // Assert: keine History-Tabelle angelegt (das macht Migrate() später)
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
        // Arrange: Schema + existierende History-Row
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
            MigrationBaseliner.EnsureBaselined(ctx, NullLogger<MigrationBaseliner>.Instance);
        }

        // Assert: kein zweiter Eintrag
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
            Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
        }
    }
}
```

- [ ] **Step 3: Test ausführen — muss ROT sein (Klasse existiert nicht)**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~MigrationBaselinerTests
```

Expected: FAIL — `MigrationBaseliner` nicht gefunden.

- [ ] **Step 4: `MigrationBaseliner` implementieren**

Erstelle `CoffeeApi/Infrastructure/MigrationBaseliner.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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

            using var cmd = conn.CreateCommand();
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
```

- [ ] **Step 5: Tests müssen jetzt grün**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~MigrationBaselinerTests
```

Expected: 3/3 PASS.

- [ ] **Step 6: Commit**

```bash
git add CoffeeApi/Infrastructure/MigrationBaseliner.cs CoffeeTest/Infrastructure/MigrationBaselinerTests.cs CoffeeTest/CoffeeTest.csproj
git commit -m "feat(db): MigrationBaseliner for automatic EF baseline on pre-migration DBs"
```

---

## Task 5: `Program.cs` auf Baseliner + Migrate umstellen

**Files:**
- Modify: `CoffeeApi/Program.cs:46-51`

- [ ] **Step 1: `Program.cs` ändern**

Finde:

```csharp
// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
```

Ersetze durch:

```csharp
// Baseline pre-migration DBs, then apply pending migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    MigrationBaseliner.EnsureBaselined(db, logger);
    db.Database.Migrate();
}
```

Füge oben in den `using`-Statements hinzu, falls nicht schon vorhanden:
```csharp
using Microsoft.Extensions.Logging;
```
(Normalerweise transitively über ASP.NET Core schon drin.)

- [ ] **Step 2: Smoke-Test-Skript**

```bash
cat > /tmp/test-migrate.sh <<'EOF'
#!/bin/bash
set -e
cd CoffeeApi

# Szenario A: leere DB → Baseliner skip, Migrate legt Schema an
DB=/tmp/coffee-fresh.db
rm -f $DB
export ConnectionStrings__Default="Data Source=$DB"
dotnet run &
PID=$!
sleep 5
curl -sf http://localhost:5000/api/health > /dev/null || (kill $PID; exit 1)
kill $PID
sleep 1
sqlite3 $DB "SELECT COUNT(*) FROM __EFMigrationsHistory;" | grep -q '^1$'
sqlite3 $DB "SELECT name FROM sqlite_master WHERE type='table' AND name='MachineSnapshots';" | grep -q MachineSnapshots
echo "Szenario A: OK"

# Szenario B: Pre-migration DB mit Daten (wie NAS Prod-DB)
DB=/tmp/coffee-preexisting.db
rm -f $DB
sqlite3 $DB '
CREATE TABLE "MachineSnapshots" (
    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "Timestamp" TEXT NOT NULL,
    "MachineId" TEXT NOT NULL DEFAULT "EQ900-DEFAULT",
    "BeverageCounterCoffee" INTEGER NOT NULL DEFAULT 0,
    "BeverageCounterCoffeeAndMilk" INTEGER NOT NULL DEFAULT 0,
    "BeverageCounterMilk" INTEGER NOT NULL DEFAULT 0,
    "BeverageCounterHotWaterCups" INTEGER NOT NULL DEFAULT 0,
    "BeverageCounterHotWater" INTEGER NOT NULL DEFAULT 0,
    "OperationState" TEXT NOT NULL DEFAULT "Ready",
    "RemoteControlAllowed" INTEGER NOT NULL DEFAULT 0,
    "LocalControlActive" INTEGER NOT NULL DEFAULT 0,
    "InteriorIlluminationActive" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TEXT NOT NULL
);
INSERT INTO MachineSnapshots (Timestamp, OperationState, CreatedAt, BeverageCounterCoffee)
VALUES (datetime("now"), "Ready", datetime("now"), 42);
'
BEFORE=$(sqlite3 $DB "SELECT COUNT(*) FROM MachineSnapshots;")

export ConnectionStrings__Default="Data Source=$DB"
dotnet run &
PID=$!
sleep 5
curl -sf http://localhost:5000/api/health > /dev/null || (kill $PID; exit 1)
kill $PID
sleep 1

AFTER=$(sqlite3 $DB "SELECT COUNT(*) FROM MachineSnapshots;")
HISTORY=$(sqlite3 $DB "SELECT COUNT(*) FROM __EFMigrationsHistory;")

if [ "$BEFORE" != "$AFTER" ]; then echo "FAIL: data count changed $BEFORE -> $AFTER"; exit 1; fi
if [ "$HISTORY" != "1" ]; then echo "FAIL: history not baselined"; exit 1; fi
echo "Szenario B: OK (baselined + data preserved)"

rm -f /tmp/coffee-fresh.db /tmp/coffee-preexisting.db
EOF
chmod +x /tmp/test-migrate.sh
/tmp/test-migrate.sh
rm /tmp/test-migrate.sh
```

Expected: `Szenario A: OK` und `Szenario B: OK (baselined + data preserved)`.

- [ ] **Step 3: Unit-Tests komplett grün**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj
```

Expected: 36 Tests grün (33 alt + 3 neu).

- [ ] **Step 4: Commit**

```bash
git add CoffeeApi/Program.cs
git commit -m "feat(db): use Baseliner+Migrate instead of EnsureCreated"
```

---

## Task 6: Dokumentation

**Files:**
- Modify: `README.md`
- Modify: `PROJECT_STATE.md`

- [ ] **Step 1: README — Abschnitt "Schema-Migrationen" anfügen**

Nach dem Abschnitt `## Datensicherung` anfügen:

```markdown
## Schema-Migrationen

Das Backend nutzt **EF Core Migrations**. Beim Container-Start wird sequentiell ausgeführt:

1. `MigrationBaseliner.EnsureBaselined()` — erkennt automatisch Pre-Migration-DBs (z.B. die urspruengliche Prod-DB von der NAS, die mit `EnsureCreated()` angelegt wurde) und seedet `__EFMigrationsHistory` mit der Initial-Migration, sodass keine Tabelle doppelt angelegt wird.
2. `Database.Migrate()` — wendet alle pending Migrations an, bestehende Daten bleiben unberuehrt.

### Neue Migration anlegen

```bash
cd CoffeeApi
dotnet ef migrations add <NameDerAenderung>
```

Beim naechsten Deploy laeuft sie beim Container-Start automatisch. Kein manueller SQL-Schritt, kein SSH noetig.

### Tests und Migrations

Tests in `CoffeeTest/` nutzen `InMemoryDatabase` — kein Migration-Support. `TestDbContextFactory.Create()` nutzt weiterhin `EnsureCreated()`. Der `MigrationBaseliner` wird separat gegen eine temporaere SQLite-Datei getestet (`MigrationBaselinerTests`).
```

- [ ] **Step 2: PROJECT_STATE — Aenderungshistorie**

Ergänze am Ende der Tabelle:

```
| 2026-04-18 | EF Core Migrations + Auto-Baseliner: sichere Schema-Rollouts ohne NAS-SSH |
```

- [ ] **Step 3: Commit**

```bash
git add README.md PROJECT_STATE.md
git commit -m "docs: EF migrations + auto-baseliner workflow"
```

---

## Self-Review

- ✅ **Spec coverage:** Initial-Migration (Task 2), Verifikation (Task 3), Baseliner mit TDD (Task 4), Program.cs-Integration (Task 5), Docs (Task 6). Der ursprüngliche Task 7 (manueller NAS-Deploy) entfällt — Auto-Baseline macht Deploy zu einem reinen Image-Restart.
- ✅ **Placeholder scan:** Keine TBDs. Alle Tests vollständig codiert.
- ✅ **Type consistency:** `MigrationBaseliner.EnsureBaselined(db, logger)` ist die einzige API-Signatur, in Tasks 4 und 5 identisch verwendet.
- ✅ **Risk mitigation:** Baseliner ist idempotent (Test 3), skipt bei frischer DB (Test 2), seedet nur bei Pre-Migration-Stand (Test 1). Alle drei Zustände mit Tests abgedeckt.
