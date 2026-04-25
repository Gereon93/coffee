# Coffee Status, Power-Toggle und Day-Event-Annotationen — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** API-Endpoint `GET /coffee/status` ergänzen, einen status-bewussten Power-Toggle in der NavBar des `coffee-dashboard` einbauen, und Tages-Annotationen für Events (Geburtstag, Besuch, ...) ermöglichen — vereinheitlicht mit dem bestehenden Massenimport-Flag in einem `MarkedDay`-Modell.

**Architecture:** Bestehendes `ExcludedDay`-Schema wird via EF-Migration in `MarkedDay` umbenannt, mit zusätzlichen Spalten `Kind` (mass-import|event) und `EventType` (birthday|visitors|…). Stats-Aggregationen filtern weiterhin nur `Kind=mass-import` raus; Frontend-Anomalie-Erkennung filtert beide Kinds. Coffee-Status-Endpoint ruft den existierenden n8n-Webhook (gleiche URL, GET statt PUT) und cached 7s. Power-Button in der NavBar ist on-demand: einmal Status holen beim Mount, nach Klick neu laden.

**Tech Stack:** ASP.NET Core .NET 10, EF Core 9 (SQLite), xUnit, React 19 + Vite, TanStack Query, Tailwind, Recharts, lucide-react.

**Spec:** `docs/superpowers/specs/2026-04-25-coffee-status-und-day-events-design.md`

---

## File Structure

### Backend (rename + new)

| File | Verantwortung |
|---|---|
| `CoffeeApi/Domain/MarkedDay.cs` (rename von ExcludedDay.cs) | Domain-Entity mit `Kind`, `EventType` |
| `CoffeeApi/Infrastructure/AppDbContext.cs` | DbSet rename + neue Spalten konfigurieren |
| `CoffeeApi/Migrations/<ts>_RenameExcludedDaysToMarkedDays.cs` (neu) | Tabelle umbenennen, `Kind`/`EventType` ergänzen |
| `CoffeeApi/DTOs/MarkedDayDto.cs` (rename + erweitern) | Response- und Create-DTOs mit Kind/EventType |
| `CoffeeApi/Controllers/MarkedDaysController.cs` (rename) | CRUD für `/api/stats/marked-days` |
| `CoffeeApi/Services/SnapshotService.cs` | Heatmap-Filter auf `Kind == "mass-import"` |
| `CoffeeApi/DTOs/CoffeeStatusDto.cs` (neu) | Status-Response-Schema |
| `CoffeeApi/Services/IHomeConnectService.cs` | + `Task<CoffeeStatusDto> GetStatusAsync()` |
| `CoffeeApi/Services/HomeConnectService.cs` | Implementation (HTTP GET auf gleichen Webhook) |
| `CoffeeApi/Controllers/CoffeeStatusController.cs` (neu) | GET /coffee/status mit IMemoryCache |
| `CoffeeApi/Program.cs` | `AddMemoryCache()` registrieren |

### Backend Tests (rename + new)

| File | Verantwortung |
|---|---|
| `CoffeeTest/Controllers/MarkedDaysControllerTests.cs` (rename) | CRUD + neue Validierungen für Kind |
| `CoffeeTest/Services/SnapshotServiceHeatmapTests.cs` | Test name + Aufrufe auf `MarkedDays` umstellen, Event-Tag wird **nicht** ausgeschlossen |
| `CoffeeTest/Controllers/CoffeeStatusControllerTests.cs` (neu) | Cache, durchreichen, n8n-Fehler |

### Frontend (rename + new)

| File | Verantwortung |
|---|---|
| `coffee-dashboard/src/api/types.ts` | Typen `MarkedDay`, `MarkedDayKind`, `EventType`, `CoffeeStatus` |
| `coffee-dashboard/src/api/stats.ts` | `fetchMarkedDays`, `addMarkedDay`, `removeMarkedDay`, `fetchCoffeeStatus`, `setCoffeePower` |
| `coffee-dashboard/src/hooks/useMarkedDays.ts` (rename) | Hook + Helper-Selektoren |
| `coffee-dashboard/src/hooks/useCoffeeStatus.ts` (neu) | Status + Mutation |
| `coffee-dashboard/src/lib/markedDayUtils.ts` (rename) | Set/Filter-Helper, jetzt kind-aware |
| `coffee-dashboard/src/lib/coffeeTimeLock.ts` (neu) | `coffeeAllowed()` (18–07h Berlin) |
| `coffee-dashboard/src/components/layout/CoffeePowerButton.tsx` (neu) | UI-Komponente |
| `coffee-dashboard/src/components/layout/NavBar.tsx` | Button einbinden |
| `coffee-dashboard/src/components/dashboard/MarkDayEventModal.tsx` (neu) | Event-Modal |
| `coffee-dashboard/src/components/charts/DailyBarChart.tsx` | Klick + Emoji-Badge + Tooltip |
| `coffee-dashboard/src/hooks/useAnomalyDetection.ts` | Auch Events aus Baseline filtern |
| `coffee-dashboard/src/pages/LogPage.tsx` | `useMarkedDays` mit kind-Filter benutzen |
| `coffee-dashboard/src/pages/DashboardPage.tsx` | Modal-State + Klick-Handler durchreichen |
| `coffee-dashboard/src/components/log/MarkAsBackfillModal.tsx` | Kleine Anpassung: `kind: "mass-import"` an POST mitgeben |

---

# Phase 1 — Backend: Domain, Migration, Stats-Filter

## Task 1: Domain — `ExcludedDay` → `MarkedDay`

**Files:**
- Rename: `CoffeeApi/Domain/ExcludedDay.cs` → `CoffeeApi/Domain/MarkedDay.cs`
- Modify: `CoffeeApi/Infrastructure/AppDbContext.cs`

- [ ] **Step 1: Domain-Klasse umbenennen + Felder ergänzen**

`git mv CoffeeApi/Domain/ExcludedDay.cs CoffeeApi/Domain/MarkedDay.cs`

Datei-Inhalt komplett ersetzen mit:

```csharp
namespace CoffeeApi.Domain;

/// <summary>
/// A day with a manual annotation. Two kinds:
/// - "mass-import": excluded from stats and anomaly detection
/// - "event":       valid data, but flagged for explanation (birthday, visitors, ...)
/// One MarkedDay per Date (kind decides semantics).
/// </summary>
public class MarkedDay
{
    /// <summary>Local-date representation (yyyy-MM-dd). Primary key.</summary>
    public DateOnly Date { get; set; }

    /// <summary>"mass-import" or "event"</summary>
    public string Kind { get; set; } = "mass-import";

    /// <summary>
    /// Required when Kind="event": "birthday"|"visitors"|"party"|"sick"|"vacation"|"other".
    /// Null for Kind="mass-import".
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>Free-text reason / note. Required for mass-import, optional for event.</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: AppDbContext umstellen**

In `CoffeeApi/Infrastructure/AppDbContext.cs`:

Zeile 17 ersetzen:
```csharp
public DbSet<MarkedDay> MarkedDays { get; set; } = null!;
```

Zeile 62-78 ersetzen (Entity-Konfiguration):
```csharp
modelBuilder.Entity<MarkedDay>(entity =>
{
    entity.HasKey(e => e.Date);

    entity.Property(e => e.Date)
        .IsRequired()
        .HasConversion(
            v => v.ToString("yyyy-MM-dd"),
            v => DateOnly.Parse(v));

    entity.Property(e => e.Kind)
        .IsRequired()
        .HasMaxLength(20)
        .HasDefaultValue("mass-import");

    entity.Property(e => e.EventType)
        .HasMaxLength(20);

    entity.Property(e => e.Reason)
        .IsRequired()
        .HasMaxLength(500);

    entity.Property(e => e.CreatedAt)
        .IsRequired();
});
```

- [ ] **Step 3: Build prüfen (es wird brechen — das ist erwartet)**

Run: `dotnet build CoffeeApi/CoffeeApi.csproj`
Expected: Compile-Fehler in `ExcludedDaysController`, `SnapshotService`, Tests — alles was `ExcludedDays`/`ExcludedDay` referenziert. Wir fixen das in Task 2-4.

- [ ] **Step 4: Commit**

```bash
git add CoffeeApi/Domain/MarkedDay.cs CoffeeApi/Infrastructure/AppDbContext.cs
git rm CoffeeApi/Domain/ExcludedDay.cs 2>/dev/null || true
git commit -m "refactor(domain): rename ExcludedDay to MarkedDay with kind+eventType"
```

---

## Task 2: DTOs umstellen

**Files:**
- Rename: `CoffeeApi/DTOs/ExcludedDayDto.cs` → `CoffeeApi/DTOs/MarkedDayDto.cs`

- [ ] **Step 1: Datei umbenennen**

```bash
git mv CoffeeApi/DTOs/ExcludedDayDto.cs CoffeeApi/DTOs/MarkedDayDto.cs
```

- [ ] **Step 2: Inhalt ersetzen**

```csharp
namespace CoffeeApi.DTOs;

public class MarkedDayDto
{
    /// <summary>Local date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>"mass-import" or "event"</summary>
    public string Kind { get; set; } = "mass-import";

    /// <summary>
    /// Required when Kind="event": birthday|visitors|party|sick|vacation|other.
    /// Null for Kind="mass-import".
    /// </summary>
    public string? EventType { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class CreateMarkedDayDto
{
    /// <summary>Local date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>"mass-import" or "event". Defaults to "mass-import" if omitted (backward-compat).</summary>
    public string Kind { get; set; } = "mass-import";

    /// <summary>Required when Kind="event"; ignored otherwise.</summary>
    public string? EventType { get; set; }

    public string Reason { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Commit**

```bash
git add CoffeeApi/DTOs/MarkedDayDto.cs
git rm CoffeeApi/DTOs/ExcludedDayDto.cs 2>/dev/null || true
git commit -m "refactor(dto): rename ExcludedDayDto to MarkedDayDto with kind+eventType"
```

---

## Task 3: Controller umstellen

**Files:**
- Rename: `CoffeeApi/Controllers/ExcludedDaysController.cs` → `CoffeeApi/Controllers/MarkedDaysController.cs`

- [ ] **Step 1: Datei umbenennen**

```bash
git mv CoffeeApi/Controllers/ExcludedDaysController.cs CoffeeApi/Controllers/MarkedDaysController.cs
```

- [ ] **Step 2: Inhalt komplett ersetzen**

```csharp
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Controllers;

[ApiController]
[Route("api/stats/marked-days")]
public class MarkedDaysController : ControllerBase
{
    private static readonly HashSet<string> ValidKinds = new() { "mass-import", "event" };
    private static readonly HashSet<string> ValidEventTypes = new()
    {
        "birthday", "visitors", "party", "sick", "vacation", "other"
    };

    private readonly AppDbContext _context;
    private readonly ILogger<MarkedDaysController> _logger;

    public MarkedDaysController(AppDbContext context, ILogger<MarkedDaysController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MarkedDayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? kind = null)
    {
        if (kind != null && !ValidKinds.Contains(kind))
        {
            return BadRequest(new { error = "Invalid kind", details = new[] { $"kind must be one of: {string.Join(", ", ValidKinds)}" } });
        }

        var query = _context.MarkedDays.AsQueryable();
        if (kind != null)
        {
            query = query.Where(d => d.Kind == kind);
        }

        var days = await query
            .OrderByDescending(d => d.Date)
            .Select(d => new MarkedDayDto
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                Kind = d.Kind,
                EventType = d.EventType,
                Reason = d.Reason,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return Ok(days);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MarkedDayDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateMarkedDayDto dto)
    {
        if (!DateOnly.TryParseExact(dto.Date, "yyyy-MM-dd", out var parsedDate))
        {
            return BadRequest(new { error = "Invalid date", details = new[] { "Use yyyy-MM-dd format" } });
        }

        var kind = string.IsNullOrWhiteSpace(dto.Kind) ? "mass-import" : dto.Kind;
        if (!ValidKinds.Contains(kind))
        {
            return BadRequest(new { error = "Invalid kind", details = new[] { $"kind must be one of: {string.Join(", ", ValidKinds)}" } });
        }

        string? eventType = null;
        if (kind == "event")
        {
            if (string.IsNullOrWhiteSpace(dto.EventType) || !ValidEventTypes.Contains(dto.EventType))
            {
                return BadRequest(new { error = "Invalid eventType", details = new[] { $"eventType must be one of: {string.Join(", ", ValidEventTypes)}" } });
            }
            eventType = dto.EventType;
        }

        // mass-import: reason is required (existing behaviour)
        // event:       reason is optional (eventType already carries semantics)
        if (kind == "mass-import" && string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new { error = "Reason required", details = new[] { "Reason must not be empty for mass-import" } });
        }

        var exists = await _context.MarkedDays.AnyAsync(d => d.Date == parsedDate);
        if (exists)
        {
            return Conflict(new { error = "Already marked", details = new[] { $"Day {dto.Date} is already marked" } });
        }

        var entity = new MarkedDay
        {
            Date = parsedDate,
            Kind = kind,
            EventType = eventType,
            Reason = (dto.Reason ?? string.Empty).Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.MarkedDays.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Day {Date} marked as {Kind}{EventType}: {Reason}",
            dto.Date, kind, eventType != null ? $"/{eventType}" : "", entity.Reason);

        var response = new MarkedDayDto
        {
            Date = entity.Date.ToString("yyyy-MM-dd"),
            Kind = entity.Kind,
            EventType = entity.EventType,
            Reason = entity.Reason,
            CreatedAt = entity.CreatedAt
        };

        return CreatedAtAction(nameof(GetAll), response);
    }

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string date)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
        {
            return BadRequest(new { error = "Invalid date", details = new[] { "Use yyyy-MM-dd format" } });
        }

        var entity = await _context.MarkedDays.FirstOrDefaultAsync(d => d.Date == parsedDate);
        if (entity == null)
        {
            return NotFound(new { error = "Not found", details = new[] { $"Day {date} is not marked" } });
        }

        _context.MarkedDays.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Day {Date} unmarked", date);
        return NoContent();
    }
}
```

- [ ] **Step 3: SnapshotService Heatmap-Filter umstellen**

In `CoffeeApi/Services/SnapshotService.cs`, Zeilen 169-171 ersetzen:

```csharp
var excludedDates = (await _context.MarkedDays
    .Where(d => d.Kind == "mass-import")
    .Select(d => d.Date)
    .ToListAsync()).ToHashSet();
```

- [ ] **Step 4: Build prüfen**

Run: `dotnet build CoffeeApi/CoffeeApi.csproj`
Expected: erfolgreich (alle Backend-Files clean). Tests werden noch brechen — fixen wir in Task 5.

- [ ] **Step 5: Commit**

```bash
git add CoffeeApi/Controllers/MarkedDaysController.cs CoffeeApi/Services/SnapshotService.cs
git rm CoffeeApi/Controllers/ExcludedDaysController.cs 2>/dev/null || true
git commit -m "refactor(api): MarkedDaysController + heatmap filter on kind=mass-import"
```

---

## Task 4: EF-Migration `RenameExcludedDaysToMarkedDays`

**Files:**
- Create: `CoffeeApi/Migrations/<timestamp>_RenameExcludedDaysToMarkedDays.cs`

- [ ] **Step 1: Migration generieren**

Run:
```bash
dotnet ef migrations add RenameExcludedDaysToMarkedDays --project CoffeeApi/CoffeeApi.csproj
```

Expected: neue Migration im `Migrations/`-Ordner. EF-Tool wird wahrscheinlich auto-detect die Tabellen-Umbenennung + neue Spalten generieren — aber vermutlich ohne sicheren Daten-Erhalt. Wir prüfen den Output und passen ihn an.

- [ ] **Step 2: Migration manuell editieren**

Die generierte Migration ÖFFNEN und sicherstellen, dass sie genau folgendes tut. Falls EF was anderes generiert, ersetze den Inhalt durch:

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeApi.Migrations
{
    public partial class RenameExcludedDaysToMarkedDays : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename table
            migrationBuilder.RenameTable(
                name: "ExcludedDays",
                newName: "MarkedDays");

            // Add Kind column with default "mass-import" (existing rows get this)
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "MarkedDays",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "mass-import");

            // Add nullable EventType column
            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "MarkedDays",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EventType", table: "MarkedDays");
            migrationBuilder.DropColumn(name: "Kind",      table: "MarkedDays");
            migrationBuilder.RenameTable(name: "MarkedDays", newName: "ExcludedDays");
        }
    }
}
```

Falls EF die `Kind` Spalte ohne `defaultValue` generiert hat → unbedingt ergänzen, sonst schlägt die Migration auf bestehender NAS-DB fehl.

- [ ] **Step 3: Migration auf leerer DB testen**

```bash
rm -f /tmp/coffee-test.db
ConnectionStrings__Default="Data Source=/tmp/coffee-test.db" dotnet run --project CoffeeApi/CoffeeApi.csproj &
APP_PID=$!
sleep 3
kill $APP_PID 2>/dev/null
sqlite3 /tmp/coffee-test.db ".schema MarkedDays"
```

Expected: Tabelle `MarkedDays` existiert mit Spalten `Date, Kind, EventType, Reason, CreatedAt`.

- [ ] **Step 4: Migration auf vorhandener Production-Schema-DB testen**

```bash
# Frische DB mit alter ExcludedDays-Tabelle (vorletzte Migration) erzeugen
rm -f /tmp/coffee-old.db
sqlite3 /tmp/coffee-old.db <<'SQL'
CREATE TABLE "__EFMigrationsHistory" ("MigrationId" TEXT PRIMARY KEY, "ProductVersion" TEXT NOT NULL);
INSERT INTO "__EFMigrationsHistory" VALUES ('20260418215802_Initial','9.0.0');
INSERT INTO "__EFMigrationsHistory" VALUES ('20260418221046_AddExcludedDays','9.0.0');
CREATE TABLE "ExcludedDays" ("Date" TEXT PRIMARY KEY, "Reason" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL);
INSERT INTO ExcludedDays VALUES ('2026-02-15', 'BSH outage', '2026-02-15T10:00:00Z');
SQL

ConnectionStrings__Default="Data Source=/tmp/coffee-old.db" dotnet run --project CoffeeApi/CoffeeApi.csproj &
APP_PID=$!
sleep 3
kill $APP_PID 2>/dev/null

sqlite3 /tmp/coffee-old.db "SELECT Date, Kind, EventType, Reason FROM MarkedDays;"
```

Expected: `2026-02-15|mass-import||BSH outage` — bestehende Row hat `Kind=mass-import`, `EventType` ist leer.

- [ ] **Step 5: Commit**

```bash
git add CoffeeApi/Migrations/
git commit -m "feat(db): migration RenameExcludedDaysToMarkedDays + Kind/EventType columns"
```

---

## Task 5: Tests umstellen

**Files:**
- Rename: `CoffeeTest/Controllers/ExcludedDaysControllerTests.cs` → `CoffeeTest/Controllers/MarkedDaysControllerTests.cs`
- Modify: `CoffeeTest/Services/SnapshotServiceHeatmapTests.cs`

- [ ] **Step 1: Datei umbenennen**

```bash
git mv CoffeeTest/Controllers/ExcludedDaysControllerTests.cs CoffeeTest/Controllers/MarkedDaysControllerTests.cs
```

- [ ] **Step 2: Inhalt ersetzen — kompletter Suite-Code**

```csharp
using CoffeeApi.Controllers;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeTest.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Controllers;

public class MarkedDaysControllerTests
{
    private static MarkedDaysController CreateController(AppDbContext db)
    {
        return new MarkedDaysController(db, NullLogger<MarkedDaysController>.Instance);
    }

    [Fact]
    public async Task GetAll_EmptyDb_ReturnsEmptyList()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<MarkedDayDto>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetAll_FilterByKind_OnlyReturnsMatching()
    {
        using var db = TestDbContextFactory.Create();
        db.MarkedDays.AddRange(
            new MarkedDay { Date = new DateOnly(2026, 2, 15), Kind = "mass-import", Reason = "x", CreatedAt = DateTime.UtcNow },
            new MarkedDay { Date = new DateOnly(2026, 2, 16), Kind = "event", EventType = "birthday", Reason = "y", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.GetAll(kind: "event");

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<MarkedDayDto>>(ok.Value);
        var only = Assert.Single(list);
        Assert.Equal("event", only.Kind);
        Assert.Equal("birthday", only.EventType);
    }

    [Fact]
    public async Task GetAll_InvalidKind_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.GetAll(kind: "wat");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_MassImport_Returns201AndPersists()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-02-15", Kind = "mass-import", Reason = "BSH API outage" };

        var result = await controller.Create(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var returned = Assert.IsType<MarkedDayDto>(created.Value);
        Assert.Equal("mass-import", returned.Kind);
        Assert.Null(returned.EventType);

        var row = Assert.Single(await db.MarkedDays.ToListAsync());
        Assert.Equal("mass-import", row.Kind);
    }

    [Fact]
    public async Task Create_Event_Returns201WithEventType()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto
        {
            Date = "2026-04-22",
            Kind = "event",
            EventType = "birthday",
            Reason = "Schwiegereltern"
        };

        var result = await controller.Create(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var returned = Assert.IsType<MarkedDayDto>(created.Value);
        Assert.Equal("event", returned.Kind);
        Assert.Equal("birthday", returned.EventType);
        Assert.Equal("Schwiegereltern", returned.Reason);
    }

    [Fact]
    public async Task Create_EventWithoutEventType_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-04-22", Kind = "event", EventType = null, Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_EventWithInvalidEventType_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-04-22", Kind = "event", EventType = "alien-abduction", Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_InvalidKind_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-04-22", Kind = "weird", Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_EventWithEmptyReason_OkBecauseEventTypeIsEnough()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-04-22", Kind = "event", EventType = "visitors", Reason = "" };

        var result = await controller.Create(dto);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Create_MassImportEmptyReason_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-02-15", Kind = "mass-import", Reason = "" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_InvalidDate_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "not-a-date", Kind = "mass-import", Reason = "x" };

        var result = await controller.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_ExistingDate_Returns409()
    {
        using var db = TestDbContextFactory.Create();
        db.MarkedDays.Add(new MarkedDay { Date = new DateOnly(2026, 2, 15), Kind = "mass-import", Reason = "first", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        var dto = new CreateMarkedDayDto { Date = "2026-02-15", Kind = "event", EventType = "birthday", Reason = "second" };

        var result = await controller.Create(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Delete_Existing_Returns204AndRemoves()
    {
        using var db = TestDbContextFactory.Create();
        db.MarkedDays.Add(new MarkedDay { Date = new DateOnly(2026, 2, 15), Kind = "mass-import", Reason = "x", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Delete("2026-02-15");

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.MarkedDays.ToListAsync());
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.Delete("2026-02-15");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Delete_InvalidDate_Returns400()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.Delete("bad-date");

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
```

- [ ] **Step 3: Heatmap-Test anpassen**

In `CoffeeTest/Services/SnapshotServiceHeatmapTests.cs` Zeile 62-67 (im Test `GetHeatmapData_SkipsDeltasOnExcludedDays`) ersetzen:

```csharp
db.MarkedDays.Add(new MarkedDay
{
    Date = DateOnly.FromDateTime(friday),
    Kind = "mass-import",
    Reason = "mass import",
    CreatedAt = DateTime.UtcNow
});
```

Zusätzlich am Ende der Datei (vor dem schließenden `}`) neuen Test einfügen:

```csharp
    [Fact]
    public async Task GetHeatmapData_DoesNotSkipEventDays()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        // Friday 20:00 baseline, Friday 21:00 +30 spike — Friday is marked as event (birthday).
        // Event days must remain in the heatmap (they are valid, just annotated).
        var friday = DateTime.UtcNow.Date;
        while (friday.DayOfWeek != DayOfWeek.Friday) friday = friday.AddDays(-1);

        db.MachineSnapshots.AddRange(
            new SnapshotBuilder().At(friday.AddHours(20)).WithCoffee(100).Build(),
            new SnapshotBuilder().At(friday.AddHours(21)).WithCoffee(130).Build()
        );
        db.MarkedDays.Add(new MarkedDay
        {
            Date = DateOnly.FromDateTime(friday),
            Kind = "event",
            EventType = "birthday",
            Reason = "Schwiegereltern",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await service.GetHeatmapDataAsync(4);

        Assert.Contains(result, h => h.DayOfWeek == 5 && h.Hour == 21 && h.Count == 30);
    }
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test CoffeeTest/CoffeeTest.csproj`
Expected: alle Tests grün (33 alte + 5 neue MarkedDay-Tests + 1 neuer Heatmap-Test).

- [ ] **Step 5: Commit**

```bash
git add CoffeeTest/
git rm CoffeeTest/Controllers/ExcludedDaysControllerTests.cs 2>/dev/null || true
git commit -m "test: MarkedDay controller + heatmap event/mass-import distinction"
```

---

# Phase 2 — Backend: Coffee Status Endpoint

## Task 6: CoffeeStatusDto

**Files:**
- Create: `CoffeeApi/DTOs/CoffeeStatusDto.cs`

- [ ] **Step 1: Datei anlegen**

```csharp
namespace CoffeeApi.DTOs;

/// <summary>
/// Live status of the coffee machine, mirrors the schema in BRIEFING_COFFEE_API.md.
/// Returned by GET /coffee/status.
/// </summary>
public class CoffeeStatusDto
{
    /// <summary>"ok" | "error". Always "ok" when the API itself is healthy (use reachable=false for BSH outages).</summary>
    public string Status { get; set; } = "ok";

    /// <summary>True if the n8n/HomeConnect chain delivered fresh data.</summary>
    public bool Reachable { get; set; }

    /// <summary>"on" | "off" | "standby". Null when reachable=false.</summary>
    public string? PowerState { get; set; }

    /// <summary>"inactive" | "ready" | "run" | "pause" | "finished" | "error". Null when reachable=false.</summary>
    public string? OperationState { get; set; }

    /// <summary>Pre-formatted German label, e.g. "Bereit", "Aus", "Heizt auf", "Offline".</summary>
    public string Label { get; set; } = "Unbekannt";

    /// <summary>UTC timestamp of the last successful HomeConnect read.</summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>Optional message — populated when reachable=false or for diagnostic info.</summary>
    public string? Message { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add CoffeeApi/DTOs/CoffeeStatusDto.cs
git commit -m "feat(dto): CoffeeStatusDto for GET /coffee/status response"
```

---

## Task 7: HomeConnectService.GetStatusAsync

**Files:**
- Modify: `CoffeeApi/Services/IHomeConnectService.cs`
- Modify: `CoffeeApi/Services/HomeConnectService.cs`

- [ ] **Step 1: Interface erweitern**

`CoffeeApi/Services/IHomeConnectService.cs` komplett ersetzen:

```csharp
using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public interface IHomeConnectService
{
    Task SetPowerStateAsync(bool on);
    Task<CoffeeStatusDto> GetStatusAsync();
}
```

- [ ] **Step 2: Implementation ergänzen**

In `CoffeeApi/Services/HomeConnectService.cs` am Ende der Klasse (vor `}`) einfügen:

```csharp
public async Task<CoffeeStatusDto> GetStatusAsync()
{
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await _httpClient.GetAsync(_webhookUrl, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("n8n status webhook returned {Status}", (int)response.StatusCode);
            return Unreachable($"Status-Service antwortete mit {(int)response.StatusCode}");
        }

        var body = await response.Content.ReadAsStringAsync(cts.Token);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<CoffeeStatusDto>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (parsed == null)
        {
            return Unreachable("Status-Antwort konnte nicht geparsed werden");
        }

        return parsed;
    }
    catch (TaskCanceledException)
    {
        _logger.LogWarning("n8n status webhook timed out");
        return Unreachable("Status-Service antwortet nicht (Timeout)");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "n8n status webhook failed");
        return Unreachable("Status-Service nicht erreichbar");
    }
}

private static CoffeeStatusDto Unreachable(string message) => new()
{
    Status = "ok",
    Reachable = false,
    PowerState = null,
    OperationState = null,
    Label = "Offline",
    LastUpdated = DateTime.UtcNow,
    Message = message
};
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build CoffeeApi/CoffeeApi.csproj`
Expected: erfolgreich.

- [ ] **Step 4: Commit**

```bash
git add CoffeeApi/Services/
git commit -m "feat(service): HomeConnectService.GetStatusAsync via existing webhook"
```

---

## Task 8: CoffeeStatusController + IMemoryCache

**Files:**
- Create: `CoffeeApi/Controllers/CoffeeStatusController.cs`
- Modify: `CoffeeApi/Program.cs`

- [ ] **Step 1: Controller anlegen**

```csharp
using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeApi.Controllers;

/// <summary>
/// GET /coffee/status — live status of the EQ900 via n8n -> HomeConnect.
/// Cached 7s to spare BSH quota.
/// </summary>
[ApiController]
[Route("coffee")]
public class CoffeeStatusController : ControllerBase
{
    private const string CacheKey = "coffee:status";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(7);

    private readonly IHomeConnectService _homeConnect;
    private readonly IMemoryCache _cache;

    public CoffeeStatusController(IHomeConnectService homeConnect, IMemoryCache cache)
    {
        _homeConnect = homeConnect;
        _cache = cache;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(CoffeeStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        if (_cache.TryGetValue(CacheKey, out CoffeeStatusDto? cached) && cached != null)
        {
            return Ok(cached);
        }

        var fresh = await _homeConnect.GetStatusAsync();
        _cache.Set(CacheKey, fresh, CacheTtl);
        return Ok(fresh);
    }
}
```

- [ ] **Step 2: IMemoryCache in Program.cs registrieren**

In `CoffeeApi/Program.cs`, nach Zeile 32 (`builder.Services.AddHttpClient<IHomeConnectService, HomeConnectService>();`) einfügen:

```csharp
            builder.Services.AddMemoryCache();
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build CoffeeApi/CoffeeApi.csproj`
Expected: erfolgreich.

- [ ] **Step 4: Commit**

```bash
git add CoffeeApi/Controllers/CoffeeStatusController.cs CoffeeApi/Program.cs
git commit -m "feat(api): GET /coffee/status with 7s in-memory cache"
```

---

## Task 9: Tests für CoffeeStatusController

**Files:**
- Create: `CoffeeTest/Controllers/CoffeeStatusControllerTests.cs`

- [ ] **Step 1: Stub-Service für Tests anlegen — am Anfang derselben Datei**

```csharp
using CoffeeApi.Controllers;
using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeTest.Controllers;

public class CoffeeStatusControllerTests
{
    private sealed class StubHomeConnect : IHomeConnectService
    {
        public int GetStatusCallCount { get; private set; }
        public Func<CoffeeStatusDto>? StatusFactory { get; set; }

        public Task SetPowerStateAsync(bool on) => Task.CompletedTask;

        public Task<CoffeeStatusDto> GetStatusAsync()
        {
            GetStatusCallCount++;
            return Task.FromResult(StatusFactory?.Invoke() ?? new CoffeeStatusDto
            {
                Status = "ok", Reachable = true, PowerState = "on",
                OperationState = "ready", Label = "Bereit", LastUpdated = DateTime.UtcNow
            });
        }
    }

    private static (CoffeeStatusController c, StubHomeConnect stub) CreateController()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var stub = new StubHomeConnect();
        return (new CoffeeStatusController(stub, cache), stub);
    }

    [Fact]
    public async Task GetStatus_ReturnsServicePayload()
    {
        var (controller, _) = CreateController();

        var result = await controller.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CoffeeStatusDto>(ok.Value);
        Assert.True(dto.Reachable);
        Assert.Equal("on", dto.PowerState);
    }

    [Fact]
    public async Task GetStatus_CachesWithinTtl()
    {
        var (controller, stub) = CreateController();

        await controller.GetStatus();
        await controller.GetStatus();
        await controller.GetStatus();

        Assert.Equal(1, stub.GetStatusCallCount);
    }

    [Fact]
    public async Task GetStatus_PassesThroughUnreachableState()
    {
        var (controller, stub) = CreateController();
        stub.StatusFactory = () => new CoffeeStatusDto
        {
            Status = "ok",
            Reachable = false,
            Label = "Offline",
            Message = "Status-Service nicht erreichbar"
        };

        var result = await controller.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CoffeeStatusDto>(ok.Value);
        Assert.False(dto.Reachable);
        Assert.Equal("Offline", dto.Label);
        Assert.Equal("Status-Service nicht erreichbar", dto.Message);
    }
}
```

- [ ] **Step 2: Tests laufen lassen**

Run: `dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~CoffeeStatusControllerTests`
Expected: 3 Tests grün.

- [ ] **Step 3: Komplette Test-Suite laufen lassen**

Run: `dotnet test CoffeeTest/CoffeeTest.csproj`
Expected: alle Tests grün.

- [ ] **Step 4: Commit**

```bash
git add CoffeeTest/Controllers/CoffeeStatusControllerTests.cs
git commit -m "test(api): CoffeeStatusController happy-path, cache, unreachable"
```

---

# Phase 3 — Frontend: Types & API

## Task 10: Frontend-Typen & API-Client

**Files:**
- Modify: `coffee-dashboard/src/api/types.ts`
- Modify: `coffee-dashboard/src/api/stats.ts`
- Create: `coffee-dashboard/src/api/coffee.ts`

- [ ] **Step 1: Types ergänzen**

In `coffee-dashboard/src/api/types.ts` die `ExcludedDay` und `CreateExcludedDayPayload` Blocks ersetzen durch:

```ts
export type MarkedDayKind = 'mass-import' | 'event';

export type EventType =
  | 'birthday'
  | 'visitors'
  | 'party'
  | 'sick'
  | 'vacation'
  | 'other';

export interface MarkedDay {
  date: string;            // yyyy-MM-dd
  kind: MarkedDayKind;
  eventType: EventType | null;
  reason: string;
  createdAt: string;       // ISO timestamp
}

export interface CreateMarkedDayPayload {
  date: string;            // yyyy-MM-dd
  kind: MarkedDayKind;
  eventType?: EventType;
  reason: string;
}

// Backward-compat alias for existing LogPage code (will be removed in Task 12)
export type ExcludedDay = MarkedDay;
export type CreateExcludedDayPayload = CreateMarkedDayPayload;

export interface CoffeeStatus {
  status: 'ok' | 'error';
  reachable: boolean;
  powerState: 'on' | 'off' | 'standby' | null;
  operationState: 'inactive' | 'ready' | 'run' | 'pause' | 'finished' | 'error' | null;
  label: string;
  lastUpdated: string;
  message?: string;
}
```

- [ ] **Step 2: stats.ts auf MarkedDays-API umstellen**

In `coffee-dashboard/src/api/stats.ts` die unteren drei Funktionen (`fetchExcludedDays`, `addExcludedDay`, `removeExcludedDay`) ersetzen durch:

```ts
import type {
  // ... existing imports
  MarkedDay,
  MarkedDayKind,
  CreateMarkedDayPayload,
} from './types';

export function fetchMarkedDays(kind?: MarkedDayKind) {
  const qs = kind ? `?kind=${kind}` : '';
  return fetchJson<MarkedDay[]>(`/api/stats/marked-days${qs}`);
}

export async function addMarkedDay(payload: CreateMarkedDayPayload): Promise<MarkedDay> {
  const res = await fetch('/api/stats/marked-days', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(body.error ?? `HTTP ${res.status}`);
  }
  return res.json();
}

export async function removeMarkedDay(date: string): Promise<void> {
  const res = await fetch(`/api/stats/marked-days/${date}`, { method: 'DELETE' });
  if (!res.ok && res.status !== 204) {
    const body = await res.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(body.error ?? `HTTP ${res.status}`);
  }
}
```

- [ ] **Step 3: Coffee-API-Client (neu) anlegen**

`coffee-dashboard/src/api/coffee.ts`:

```ts
import { fetchJson } from './client';
import type { CoffeeStatus } from './types';

export function fetchCoffeeStatus() {
  return fetchJson<CoffeeStatus>('/coffee/status');
}

export async function setCoffeePower(state: 'on' | 'off'): Promise<void> {
  const res = await fetch('/coffee/power', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ state }),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({ message: `HTTP ${res.status}` }));
    throw new Error(body.message ?? `HTTP ${res.status}`);
  }
}
```

- [ ] **Step 4: Type-Check**

Run: `cd coffee-dashboard && npx tsc --noEmit`
Expected: 0 Fehler. (Hooks/Components nutzen die Backward-Compat-Aliase noch.)

- [ ] **Step 5: Commit**

```bash
git add coffee-dashboard/src/api/
git commit -m "feat(api): MarkedDay types + coffee status/power client"
```

---

## Task 11: useMarkedDays + useCoffeeStatus Hooks

**Files:**
- Modify: `coffee-dashboard/src/hooks/useExcludedDays.ts` → rename via `git mv` to `useMarkedDays.ts`
- Create: `coffee-dashboard/src/hooks/useCoffeeStatus.ts`
- Modify: `coffee-dashboard/src/lib/excludedDayUtils.ts` → rename to `markedDayUtils.ts`

- [ ] **Step 1: useMarkedDays.ts**

```bash
git mv coffee-dashboard/src/hooks/useExcludedDays.ts coffee-dashboard/src/hooks/useMarkedDays.ts
```

Inhalt komplett ersetzen:

```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchMarkedDays,
  addMarkedDay,
  removeMarkedDay,
} from '../api/stats';
import type { CreateMarkedDayPayload, MarkedDayKind } from '../api/types';

const QUERY_KEY = ['marked-days'] as const;

export function useMarkedDays(kind?: MarkedDayKind) {
  return useQuery({
    queryKey: kind ? ['marked-days', kind] : QUERY_KEY,
    queryFn: () => fetchMarkedDays(kind),
    staleTime: 60_000,
  });
}

export function useAddMarkedDay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateMarkedDayPayload) => addMarkedDay(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['marked-days'] });
      qc.invalidateQueries({ queryKey: ['range'] });
      qc.invalidateQueries({ queryKey: ['daily'] });
    },
  });
}

export function useRemoveMarkedDay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (date: string) => removeMarkedDay(date),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['marked-days'] });
      qc.invalidateQueries({ queryKey: ['range'] });
      qc.invalidateQueries({ queryKey: ['daily'] });
    },
  });
}

// Backward-compat aliases — temporarily preserved for callers in LogPage etc.
// These will be inlined in Task 14.
export { useMarkedDays as useExcludedDays };
export { useAddMarkedDay as useAddExcludedDay };
export { useRemoveMarkedDay as useRemoveExcludedDay };
```

- [ ] **Step 2: markedDayUtils.ts (rename + erweitern)**

```bash
git mv coffee-dashboard/src/lib/excludedDayUtils.ts coffee-dashboard/src/lib/markedDayUtils.ts
```

Inhalt ersetzen:

```ts
import type { DailyAggregate, MarkedDay } from '../api/types';

export interface MarkedDayMaps {
  byDate: Map<string, MarkedDay>;
  massImportDates: Set<string>;
  eventDates: Set<string>;
  /** Union of mass-import + event — for anomaly-detection baseline filter. */
  allMarkedDates: Set<string>;
}

export function buildMarkedDayMaps(marked: MarkedDay[] | undefined): MarkedDayMaps {
  const byDate = new Map<string, MarkedDay>();
  const massImportDates = new Set<string>();
  const eventDates = new Set<string>();
  const allMarkedDates = new Set<string>();

  for (const m of marked ?? []) {
    byDate.set(m.date, m);
    allMarkedDates.add(m.date);
    if (m.kind === 'mass-import') massImportDates.add(m.date);
    else if (m.kind === 'event') eventDates.add(m.date);
  }

  return { byDate, massImportDates, eventDates, allMarkedDates };
}

// Backward-compat helpers used by LogPage; will be inlined in Task 14.
export function buildExcludedSet(marked: MarkedDay[] | undefined): Set<string> {
  return buildMarkedDayMaps(marked).massImportDates;
}

export function isExcluded(date: string, excludedSet: Set<string>): boolean {
  return excludedSet.has(date);
}

export function filterNonExcluded(
  days: DailyAggregate[] | undefined,
  excludedSet: Set<string>,
): DailyAggregate[] {
  return (days ?? []).filter((d) => !excludedSet.has(d.date));
}
```

- [ ] **Step 3: useCoffeeStatus.ts**

```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchCoffeeStatus, setCoffeePower } from '../api/coffee';

const QUERY_KEY = ['coffee', 'status'] as const;

export function useCoffeeStatus() {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: fetchCoffeeStatus,
    // On-demand semantics: no auto-refetch, no polling.
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnMount: true,
    retry: 0,
  });
}

export function useSetCoffeePower() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (state: 'on' | 'off') => setCoffeePower(state),
    onSuccess: () => {
      // Wait ~3s for BSH to settle, then refresh status.
      window.setTimeout(() => {
        qc.invalidateQueries({ queryKey: QUERY_KEY });
      }, 3000);
    },
  });
}
```

- [ ] **Step 4: Type-Check**

Run: `cd coffee-dashboard && npx tsc --noEmit`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add coffee-dashboard/src/hooks/ coffee-dashboard/src/lib/
git rm coffee-dashboard/src/hooks/useExcludedDays.ts 2>/dev/null || true
git rm coffee-dashboard/src/lib/excludedDayUtils.ts 2>/dev/null || true
git commit -m "feat(hooks): useMarkedDays + useCoffeeStatus, kind-aware util maps"
```

---

# Phase 4 — Frontend: Power-Toggle in NavBar

## Task 12: Time-Lock Helper

**Files:**
- Create: `coffee-dashboard/src/lib/coffeeTimeLock.ts`

- [ ] **Step 1: Helper anlegen**

```ts
/**
 * Returns true if the coffee machine may be operated right now.
 * Locked window: 18:00–07:00 local Berlin time.
 *
 * The coffee-api itself enforces nothing — this is UI safety against
 * accidental switches outside coffee hours.
 */
export function coffeeAllowed(now: Date = new Date()): boolean {
  const berlinHour = parseInt(
    new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit',
      hour12: false,
      timeZone: 'Europe/Berlin',
    }).format(now),
    10,
  );
  return berlinHour >= 7 && berlinHour < 18;
}
```

- [ ] **Step 2: Type-Check**

Run: `cd coffee-dashboard && npx tsc --noEmit`
Expected: 0 Fehler.

- [ ] **Step 3: Commit**

```bash
git add coffee-dashboard/src/lib/coffeeTimeLock.ts
git commit -m "feat(lib): coffeeAllowed time-lock helper for 18-07h Berlin window"
```

---

## Task 13: CoffeePowerButton-Komponente

**Files:**
- Create: `coffee-dashboard/src/components/layout/CoffeePowerButton.tsx`

- [ ] **Step 1: Komponente anlegen**

```tsx
import { Coffee, CoffeeOff } from 'lucide-react';
import { useCoffeeStatus, useSetCoffeePower } from '../../hooks/useCoffeeStatus';
import { coffeeAllowed } from '../../lib/coffeeTimeLock';
import type { CoffeeStatus } from '../../api/types';

interface ButtonState {
  label: string;
  disabled: boolean;
  className: string;
  icon: typeof Coffee;
  nextAction: 'on' | 'off' | null;
  title?: string;
}

const BASE = 'inline-flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm font-medium transition-colors';
const STONE = 'border-stone-300 text-stone-700 hover:bg-stone-100 dark:border-stone-700 dark:text-stone-300 dark:hover:bg-stone-800';
const EMERALD = 'border-emerald-400 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:border-emerald-700 dark:bg-emerald-950 dark:text-emerald-300';
const SKY = 'border-sky-400 bg-sky-50 text-sky-700 dark:border-sky-700 dark:bg-sky-950 dark:text-sky-300';
const AMBER = 'border-amber-400 bg-amber-50 text-amber-700 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-300';
const GREY = 'border-stone-300 text-stone-400 dark:border-stone-700 dark:text-stone-500 cursor-not-allowed';

function deriveState(status: CoffeeStatus | undefined, isMutating: boolean): ButtonState {
  if (isMutating) {
    return { label: 'Schaltet…', disabled: true, className: `${BASE} ${AMBER}`, icon: Coffee, nextAction: null };
  }
  if (!coffeeAllowed()) {
    return { label: 'Gesperrt', disabled: true, className: `${BASE} ${GREY}`, icon: Coffee, nextAction: null, title: 'Coffee-Hours: 07:00–18:00' };
  }
  if (!status || !status.reachable) {
    return { label: 'Offline', disabled: true, className: `${BASE} ${GREY}`, icon: CoffeeOff, nextAction: null, title: status?.message ?? 'Maschine nicht erreichbar' };
  }
  if (status.powerState === 'on' && status.operationState === 'run') {
    return { label: 'Läuft', disabled: true, className: `${BASE} ${SKY}`, icon: Coffee, nextAction: null, title: 'Brühvorgang läuft' };
  }
  if (status.powerState === 'on') {
    return { label: 'Ausschalten', disabled: false, className: `${BASE} ${EMERALD}`, icon: Coffee, nextAction: 'off', title: status.label };
  }
  // off / standby / null powerState
  return { label: 'Einschalten', disabled: false, className: `${BASE} ${STONE}`, icon: Coffee, nextAction: 'on', title: status.label };
}

export function CoffeePowerButton() {
  const { data: status } = useCoffeeStatus();
  const mutation = useSetCoffeePower();

  const state = deriveState(status, mutation.isPending);

  const handleClick = () => {
    if (state.nextAction) {
      mutation.mutate(state.nextAction);
    }
  };

  const Icon = state.icon;

  return (
    <button
      type="button"
      onClick={handleClick}
      disabled={state.disabled}
      className={state.className}
      title={state.title}
      aria-label={`Kaffeemaschine: ${state.label}`}
    >
      <Icon className="h-4 w-4" />
      {state.label}
    </button>
  );
}
```

- [ ] **Step 2: Type-Check**

Run: `cd coffee-dashboard && npx tsc --noEmit`
Expected: 0 Fehler.

- [ ] **Step 3: Commit**

```bash
git add coffee-dashboard/src/components/layout/CoffeePowerButton.tsx
git commit -m "feat(ui): CoffeePowerButton with status-aware states"
```

---

## Task 14: NavBar-Integration

**Files:**
- Modify: `coffee-dashboard/src/components/layout/NavBar.tsx`

- [ ] **Step 1: Import + Einbau**

In `coffee-dashboard/src/components/layout/NavBar.tsx`:

Zeile 2 ersetzen mit:
```ts
import { LayoutDashboard, Grid3x3, ScrollText, Moon, Sun } from 'lucide-react';
import { CoffeePowerButton } from './CoffeePowerButton';
```

Den Theme-Toggle-Button (Zeile 42-50) ersetzen durch ein Container-`<div>` mit beiden Buttons:

```tsx
        <div className="ml-auto flex items-center gap-2">
          <CoffeePowerButton />
          <button
            type="button"
            onClick={onToggleTheme}
            className="inline-flex items-center gap-2 rounded-md border border-stone-300 px-3 py-1.5 text-sm text-stone-700 transition-colors hover:bg-stone-100 dark:border-stone-700 dark:text-stone-300 dark:hover:bg-stone-800"
            aria-label={isDarkMode ? 'Zum hellen Modus wechseln' : 'Zum dunklen Modus wechseln'}
          >
            {isDarkMode ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            {isDarkMode ? 'Hell' : 'Dunkel'}
          </button>
        </div>
```

(Das `ml-auto` ist vom Theme-Button auf den Container gewandert.)

- [ ] **Step 2: Manuelle UI-Probe (Dev-Server)**

Run: `cd coffee-dashboard && npm run dev`

Im Browser auf `http://localhost:5173` (oder gemeldeter Port):
- NavBar zeigt rechts den Coffee-Button neben dem Theme-Toggle.
- Wenn kein Backend läuft, sollte der Button binnen ~5s auf „Offline" springen (Status-Fetch schlägt fehl).
- Vorhandenes Backend mit funktionierendem n8n: Status zeigt aktuellen Maschinenzustand.

- [ ] **Step 3: Type-Check + Build**

Run: `cd coffee-dashboard && npx tsc --noEmit && npm run build`
Expected: erfolgreich.

- [ ] **Step 4: Commit**

```bash
git add coffee-dashboard/src/components/layout/NavBar.tsx
git commit -m "feat(ui): wire CoffeePowerButton into NavBar next to theme toggle"
```

---

# Phase 5 — Frontend: Day-Event-Annotationen

## Task 15: MarkDayEventModal-Komponente

**Files:**
- Create: `coffee-dashboard/src/components/dashboard/MarkDayEventModal.tsx`

- [ ] **Step 1: Sicherstellen, dass das `dashboard/` Subdir existiert**

```bash
mkdir -p coffee-dashboard/src/components/dashboard
```

- [ ] **Step 2: Modal anlegen**

```tsx
import { useState, useEffect } from 'react';
import { X, Trash2 } from 'lucide-react';
import { useAddMarkedDay, useRemoveMarkedDay } from '../../hooks/useMarkedDays';
import type { EventType, MarkedDay } from '../../api/types';

interface QuickPick {
  type: EventType;
  emoji: string;
  label: string;
}

const QUICK_PICKS: QuickPick[] = [
  { type: 'birthday', emoji: '🎂', label: 'Geburtstag' },
  { type: 'visitors', emoji: '👥', label: 'Besuch' },
  { type: 'party',    emoji: '🎉', label: 'Feier' },
  { type: 'sick',     emoji: '🏥', label: 'Krank' },
  { type: 'vacation', emoji: '✈️', label: 'Urlaub' },
  { type: 'other',    emoji: '📌', label: 'Sonstiges' },
];

export function emojiForEventType(t: EventType): string {
  return QUICK_PICKS.find((p) => p.type === t)?.emoji ?? '📌';
}

interface Props {
  date: string;          // yyyy-MM-dd
  displayDate: string;   // "Mi 22.04.2026"
  existing: MarkedDay | null;
  open: boolean;
  onClose: () => void;
}

export function MarkDayEventModal({ date, displayDate, existing, open, onClose }: Props) {
  const isMassImport = existing?.kind === 'mass-import';
  const existingEvent = existing?.kind === 'event' ? existing : null;

  const [selected, setSelected] = useState<EventType | null>(existingEvent?.eventType ?? null);
  const [note, setNote] = useState(existingEvent?.reason ?? '');
  const [error, setError] = useState<string | null>(null);

  const addMutation = useAddMarkedDay();
  const removeMutation = useRemoveMarkedDay();

  useEffect(() => {
    if (open) {
      setSelected(existingEvent?.eventType ?? null);
      setNote(existingEvent?.reason ?? '');
      setError(null);
    }
  }, [open, existingEvent?.eventType, existingEvent?.reason]);

  if (!open) return null;

  const isPending = addMutation.isPending || removeMutation.isPending;

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selected) {
      setError('Bitte einen Anlass wählen.');
      return;
    }
    try {
      // If existing event annotation: delete first, then add (no PUT endpoint).
      if (existingEvent) {
        await removeMutation.mutateAsync(date);
      }
      await addMutation.mutateAsync({
        date,
        kind: 'event',
        eventType: selected,
        reason: note.trim(),
      });
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unbekannter Fehler');
    }
  };

  const handleRemove = async () => {
    setError(null);
    try {
      await removeMutation.mutateAsync(date);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unbekannter Fehler');
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-xl border border-stone-200 bg-white p-6 shadow-xl dark:border-stone-800 dark:bg-stone-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold">Tag markieren — {displayDate}</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-stone-400 hover:bg-stone-100 hover:text-stone-600 dark:hover:bg-stone-800"
            aria-label="Schliessen"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {isMassImport ? (
          <div className="space-y-3">
            <p className="text-sm text-stone-600 dark:text-stone-400">
              Dieser Tag ist als <strong>Massenimport</strong> markiert
              {existing?.reason ? ` (Grund: ${existing.reason})` : ''}.
              Massenimport-Markierungen können nur über die Log-Seite entfernt werden.
            </p>
            <div className="flex justify-end">
              <button
                type="button"
                onClick={onClose}
                className="rounded-md border border-stone-300 px-3 py-1.5 text-sm font-medium hover:bg-stone-100 dark:border-stone-700 dark:hover:bg-stone-800"
              >
                Schliessen
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSave} className="space-y-4">
            <p className="text-sm text-stone-600 dark:text-stone-400">
              Erkläre den Verbrauch an diesem Tag. Markierte Event-Tage werden nicht als Anomalie
              gewertet, bleiben aber in den Statistiken.
            </p>

            <div className="grid grid-cols-3 gap-2">
              {QUICK_PICKS.map((pick) => {
                const active = selected === pick.type;
                return (
                  <button
                    key={pick.type}
                    type="button"
                    onClick={() => setSelected(pick.type)}
                    className={`flex flex-col items-center gap-1 rounded-md border px-2 py-2 text-xs font-medium transition-colors ${
                      active
                        ? 'border-coffee-500 bg-coffee-50 text-coffee-700 dark:border-coffee-400 dark:bg-coffee-950 dark:text-coffee-200'
                        : 'border-stone-200 hover:bg-stone-50 dark:border-stone-700 dark:hover:bg-stone-800'
                    }`}
                  >
                    <span className="text-xl">{pick.emoji}</span>
                    {pick.label}
                  </button>
                );
              })}
            </div>

            <div>
              <label htmlFor="note" className="mb-1 block text-sm font-medium">
                Notiz (optional)
              </label>
              <input
                id="note"
                type="text"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="z.B. Schwiegereltern da"
                className="w-full rounded-md border border-stone-300 bg-white px-3 py-2 text-sm focus:border-coffee-500 focus:outline-none focus:ring-1 focus:ring-coffee-500 dark:border-stone-700 dark:bg-stone-800"
                maxLength={500}
              />
            </div>

            {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}

            <div className="flex items-center justify-between gap-2">
              <div>
                {existingEvent && (
                  <button
                    type="button"
                    onClick={handleRemove}
                    disabled={isPending}
                    className="inline-flex items-center gap-1 rounded-md border border-red-300 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                  >
                    <Trash2 className="h-3.5 w-3.5" /> Entfernen
                  </button>
                )}
              </div>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={onClose}
                  className="rounded-md border border-stone-300 px-3 py-1.5 text-sm font-medium hover:bg-stone-100 dark:border-stone-700 dark:hover:bg-stone-800"
                  disabled={isPending}
                >
                  Abbrechen
                </button>
                <button
                  type="submit"
                  disabled={isPending || !selected}
                  className="rounded-md bg-coffee-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-coffee-700 disabled:opacity-50"
                >
                  {isPending ? 'Speichere…' : existingEvent ? 'Aktualisieren' : 'Markieren'}
                </button>
              </div>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Type-Check**

Run: `cd coffee-dashboard && npx tsc --noEmit`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add coffee-dashboard/src/components/dashboard/
git commit -m "feat(ui): MarkDayEventModal with quick-picks and edit/remove flows"
```

---

## Task 16: DailyBarChart — Klick + Badges + Tooltip

**Files:**
- Modify: `coffee-dashboard/src/components/charts/DailyBarChart.tsx`

- [ ] **Step 1: DailyBarChart komplett ersetzen**

```tsx
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell, Customized,
} from 'recharts';
import type { DailyAggregate, MarkedDay } from '../../api/types';
import type { AnomalyResult } from '../../lib/anomalyUtils';
import { formatDate } from '../../lib/dateUtils';
import { emojiForEventType } from '../dashboard/MarkDayEventModal';

interface Props {
  data: DailyAggregate[];
  anomalies: AnomalyResult[];
  excludedSet: Set<string>;
  eventByDate: Map<string, MarkedDay>;
  onBarClick?: (date: string) => void;
}

export function DailyBarChart({
  data, anomalies, excludedSet, eventByDate, onBarClick,
}: Props) {
  const anomalyDates = new Set(
    anomalies.filter((a) => a.isAnomaly).map((a) => a.date),
  );

  const chartData = data.map((d) => {
    const event = eventByDate.get(d.date);
    return {
      ...d,
      label: formatDate(d.date),
      isAnomaly: anomalyDates.has(d.date),
      isExcluded: excludedSet.has(d.date),
      event: event && event.kind === 'event' ? event : null,
    };
  });

  type Entry = typeof chartData[number];

  const getCoffeeFill = (entry: Entry) => {
    if (entry.isExcluded) return '#a8a29e';
    if (entry.isAnomaly) return '#ef4444';
    return '#d97706';
  };
  const getMilkFill = (entry: Entry) => {
    if (entry.isExcluded) return '#d6d3d1';
    if (entry.isAnomaly) return '#fca5a5';
    return '#3b82f6';
  };
  const getStroke = (entry: Entry) => {
    if (entry.isExcluded) return '#78716c';
    if (entry.isAnomaly) return '#dc2626';
    return 'none';
  };

  // Custom layer that renders an emoji on top of each bar that has an event annotation.
  // Uses Recharts internal CategoricalChartProps via the Customized component.
  const EventBadges = (props: unknown) => {
    const p = props as {
      formattedGraphicalItems?: Array<{
        props: { data: Entry[] };
        item: { props: { dataKey: string } };
      }>;
      xAxisMap?: Record<string, { scale: (v: string) => number; bandwidth?: () => number }>;
      yAxisMap?: Record<string, { scale: (v: number) => number }>;
    };

    const x = p.xAxisMap ? Object.values(p.xAxisMap)[0] : null;
    const y = p.yAxisMap ? Object.values(p.yAxisMap)[0] : null;
    if (!x || !y) return null;

    const bandwidth = typeof x.bandwidth === 'function' ? x.bandwidth() : 24;

    return (
      <g>
        {chartData.map((entry, i) => {
          if (!entry.event) return null;
          const cx = x.scale(entry.label) + bandwidth / 2;
          const cy = y.scale(entry.total) - 8;
          if (Number.isNaN(cx) || Number.isNaN(cy)) return null;
          return (
            <text
              key={i}
              x={cx}
              y={cy}
              textAnchor="middle"
              fontSize={14}
              style={{ pointerEvents: 'none' }}
            >
              {emojiForEventType(entry.event.eventType ?? 'other')}
            </text>
          );
        })}
      </g>
    );
  };

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
        Taeglicher Verbrauch
      </h3>
      <ResponsiveContainer width="100%" height={300}>
        <BarChart
          data={chartData}
          onClick={(state: unknown) => {
            const s = state as { activePayload?: Array<{ payload?: Entry }> } | null;
            const payload = s?.activePayload?.[0]?.payload;
            if (payload && onBarClick) onBarClick(payload.date);
          }}
        >
          <XAxis
            dataKey="label"
            tick={{ fontSize: 12 }}
            stroke="currentColor"
            className="text-stone-400"
          />
          <YAxis
            tick={{ fontSize: 12 }}
            stroke="currentColor"
            className="text-stone-400"
            allowDecimals={false}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: 'var(--color-stone-900, #1c1917)',
              border: 'none',
              borderRadius: '0.5rem',
              color: '#e7e5e4',
              fontSize: '0.875rem',
            }}
            itemStyle={{ color: '#e7e5e4' }}
            labelStyle={{ color: '#a8a29e' }}
            formatter={(value?: number | string, name?: string, item?: { payload?: Entry }) => {
              const label = name === 'coffeeCount' ? 'Kaffee' : 'Milch';
              if (item?.payload?.isExcluded) {
                return [`${value ?? 0} (Massenimport)`, label];
              }
              if (item?.payload?.event && name === 'milkCount') {
                const e = item.payload.event;
                const note = e.reason ? ` — ${e.reason}` : '';
                return [`${value ?? 0}  ${emojiForEventType(e.eventType ?? 'other')}${note}`, label];
              }
              return [value ?? 0, label];
            }}
          />
          <Bar dataKey="coffeeCount" stackId="a" radius={[0, 0, 0, 0]} cursor="pointer">
            {chartData.map((entry, i) => (
              <Cell
                key={i}
                fill={getCoffeeFill(entry)}
                stroke={getStroke(entry)}
                strokeWidth={entry.isAnomaly || entry.isExcluded ? 2 : 0}
              />
            ))}
          </Bar>
          <Bar dataKey="milkCount" stackId="a" radius={[4, 4, 0, 0]} cursor="pointer">
            {chartData.map((entry, i) => (
              <Cell
                key={i}
                fill={getMilkFill(entry)}
                stroke={getStroke(entry)}
                strokeWidth={entry.isAnomaly || entry.isExcluded ? 2 : 0}
              />
            ))}
          </Bar>
          <Customized component={EventBadges} />
        </BarChart>
      </ResponsiveContainer>
      <div className="mt-3 flex flex-wrap items-center gap-4 text-xs text-stone-500 dark:text-stone-400">
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-amber-600" /> Kaffee
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-blue-500" /> Milch
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-red-500" /> Anomalie
        </span>
        <span className="flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded-sm bg-stone-400" /> Massenimport
        </span>
        <span className="flex items-center gap-1">
          <span>🎂</span> Event (klickbar zum Markieren)
        </span>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Type-Check**

Run: `cd coffee-dashboard && npx tsc --noEmit`
Expected: Es wird Fehler in `DashboardPage` geben (neue Pflicht-Props `eventByDate` + `onBarClick`). Das fixen wir in Task 17.

- [ ] **Step 3: Commit**

```bash
git add coffee-dashboard/src/components/charts/DailyBarChart.tsx
git commit -m "feat(chart): clickable bars + emoji badges + event-aware tooltip"
```

---

## Task 17: DashboardPage — Modal-State + Klick-Handler

**Files:**
- Read first: `coffee-dashboard/src/pages/DashboardPage.tsx`
- Modify: `coffee-dashboard/src/pages/DashboardPage.tsx`

- [ ] **Step 1: DashboardPage öffnen und aktuellen Inhalt anzeigen**

Run: `cat coffee-dashboard/src/pages/DashboardPage.tsx`

Notiere: an welcher Stelle die `useExcludedDays`-Calls sind, wo `<DailyBarChart>` gerendert wird, welche Props aktuell übergeben werden.

- [ ] **Step 2: Imports & Hook-Aufrufe ergänzen**

Ersetze die `useExcludedDays`-Importe + `excludedSet`-Berechnung durch:

```tsx
import { useState } from 'react';
import { useMarkedDays } from '../hooks/useMarkedDays';
import { buildMarkedDayMaps } from '../lib/markedDayUtils';
import { MarkDayEventModal } from '../components/dashboard/MarkDayEventModal';

// ... innerhalb der Komponente:
const { data: marked } = useMarkedDays();
const { byDate, massImportDates, allMarkedDates } = buildMarkedDayMaps(marked);
const [modalDate, setModalDate] = useState<string | null>(null);
```

Wo bisher `excludedSet` an `<DailyBarChart>` und `useAnomalyDetection` weitergegeben wurde:
- `useAnomalyDetection(rangeData, allMarkedDates, ...)` (statt `excludedSet`)
- `<DailyBarChart data={...} anomalies={...} excludedSet={massImportDates} eventByDate={byDate} onBarClick={setModalDate} />`

Am Ende des JSX (vor dem schließenden Wrapper):

```tsx
{modalDate && (
  <MarkDayEventModal
    date={modalDate}
    displayDate={formatDisplayDate(modalDate)}
    existing={byDate.get(modalDate) ?? null}
    open={true}
    onClose={() => setModalDate(null)}
  />
)}
```

`formatDisplayDate` ist in `LogPage.tsx` schon definiert — Helper besser extrahieren in `lib/dateUtils.ts`. Falls `dateUtils.ts` noch keine `formatDisplayDate` hat:

```ts
// in coffee-dashboard/src/lib/dateUtils.ts ergänzen:
export function formatDisplayDate(dateKey: string): string {
  const [y, m, d] = dateKey.split('-');
  return `${d}.${m}.${y}`;
}
```

Dann sowohl in `DashboardPage` als auch in `LogPage` aus `lib/dateUtils` importieren (LogPage-Inline-Helper kann dann gelöscht werden).

- [ ] **Step 3: Type-Check + Build**

Run: `cd coffee-dashboard && npx tsc --noEmit && npm run build`
Expected: erfolgreich.

- [ ] **Step 4: Commit**

```bash
git add coffee-dashboard/src/pages/ coffee-dashboard/src/lib/dateUtils.ts
git commit -m "feat(dashboard): clickable bars open MarkDayEventModal, anomaly filters both kinds"
```

---

## Task 18: useAnomalyDetection — Events filtern

**Files:**
- Modify: `coffee-dashboard/src/hooks/useAnomalyDetection.ts`

- [ ] **Step 1: Hook-Signatur überprüfen**

Aktueller Stand (Zeilen 5-15):

```tsx
export function useAnomalyDetection(
  data: DailyAggregate[] | undefined,
  excludedSet: Set<string>,
  threshold = 1.5,
) {
  return useMemo(() => {
    if (!data) return [];
    const filtered = data.filter((d) => !excludedSet.has(d.date));
    return detectAnomalies(filtered, threshold);
  }, [data, excludedSet, threshold]);
}
```

Der Hook nimmt schon ein `Set<string>` — wir geben jetzt `allMarkedDates` rein (aus Task 17 bereits passiert). Der Hook selbst muss nicht geändert werden, **aber** der Parameter-Name in der Signatur sollte semantisch passen. Optional umbenennen:

```tsx
export function useAnomalyDetection(
  data: DailyAggregate[] | undefined,
  excludedFromAnomaly: Set<string>,
  threshold = 1.5,
) {
  return useMemo(() => {
    if (!data) return [];
    const filtered = data.filter((d) => !excludedFromAnomaly.has(d.date));
    return detectAnomalies(filtered, threshold);
  }, [data, excludedFromAnomaly, threshold]);
}
```

(Reines Renaming, keine Logikänderung. Aufrufer in DashboardPage passt bereits durch Task 17.)

- [ ] **Step 2: Type-Check**

Run: `cd coffee-dashboard && npx tsc --noEmit`
Expected: 0 Fehler.

- [ ] **Step 3: Commit**

```bash
git add coffee-dashboard/src/hooks/useAnomalyDetection.ts
git commit -m "refactor(hook): rename excludedSet → excludedFromAnomaly for clarity"
```

---

## Task 19: LogPage — auf MarkedDays mit kind-Filter umstellen

**Files:**
- Modify: `coffee-dashboard/src/pages/LogPage.tsx`
- Modify: `coffee-dashboard/src/components/log/MarkAsBackfillModal.tsx`

- [ ] **Step 1: LogPage-Imports anpassen**

In `coffee-dashboard/src/pages/LogPage.tsx`:

Zeile 4 ersetzen:
```tsx
import { useMarkedDays, useRemoveMarkedDay } from '../hooks/useMarkedDays';
```

Zeile 8 (Import von `buildExcludedSet`) ersetzen durch:
```tsx
import { buildMarkedDayMaps } from '../lib/markedDayUtils';
```

Zeilen 53-54 (excluded-Variable & excludedSet-Build) ersetzen durch:
```tsx
const { data: marked } = useMarkedDays('mass-import');
const removeMutation = useRemoveMarkedDay();
const { massImportDates: excludedSet } = buildMarkedDayMaps(marked);
```

- [ ] **Step 2: MarkAsBackfillModal POST-Payload anpassen**

In `coffee-dashboard/src/components/log/MarkAsBackfillModal.tsx`:

Zeile 3 (Hook-Import) ersetzen:
```tsx
import { useAddMarkedDay } from '../../hooks/useMarkedDays';
```

Zeile 15 (Mutation-Hook) ersetzen:
```tsx
const mutation = useAddMarkedDay();
```

Zeile 33 (Mutation-Call) ersetzen:
```tsx
await mutation.mutateAsync({ date, kind: 'mass-import', reason: reason.trim() });
```

- [ ] **Step 3: Backward-Compat-Aliase aus Hooks entfernen**

In `coffee-dashboard/src/hooks/useMarkedDays.ts` die drei `export { ... as useExcluded... }`-Zeilen am Ende löschen.

In `coffee-dashboard/src/api/types.ts` die `ExcludedDay` und `CreateExcludedDayPayload` Aliase löschen.

Alle Usages dieser Aliase sind jetzt durch echte Imports ersetzt — Type-Check fängt verbliebene ab.

- [ ] **Step 4: Type-Check + Build**

Run: `cd coffee-dashboard && npx tsc --noEmit && npm run build`
Expected: erfolgreich. Falls Fehler — die meldenden Dateien zeigen, wo noch alte Namen verwendet werden, dann auf `Marked*` umstellen.

- [ ] **Step 5: Commit**

```bash
git add coffee-dashboard/src/
git commit -m "refactor: drop ExcludedDay backward-compat aliases, LogPage uses kind=mass-import filter"
```

---

# Phase 6 — Wrap-up & Smoke-Test

## Task 20: End-to-End Smoke-Test im Browser

**Files:** keine — manueller Test.

- [ ] **Step 1: Backend lokal starten**

```bash
cd /Users/gereon/Code/worktrees/hungry-times-invite-38f
dotnet run --project CoffeeApi/CoffeeApi.csproj
```

Expected: Logs zeigen Migration `RenameExcludedDaysToMarkedDays` applied (oder bereits applied), API hört auf Port aus appsettings.

- [ ] **Step 2: API direkt prüfen**

In separatem Terminal:
```bash
curl -s http://localhost:5000/api/stats/marked-days | jq
curl -s http://localhost:5000/coffee/status | jq
```

Expected:
- `marked-days` → `[]` oder existing rows mit `kind:"mass-import"`.
- `coffee/status` → `{"reachable": false, "label": "Offline", ...}` (n8n nicht konfiguriert lokal — das ist OK).

- [ ] **Step 3: Frontend dev server**

```bash
cd coffee-dashboard
npm run dev
```

Browser auf gemeldeter URL.

- [ ] **Step 4: NavBar-Power-Button visuell prüfen**

- Button erscheint rechts neben Theme-Toggle.
- Innerhalb 5s zeigt er „Offline" (n8n nicht erreichbar lokal). 
- Außerhalb 07–18 Uhr Berlin: zeigt „Gesperrt" (mit Tooltip).
- Hover: Tooltip sichtbar.

- [ ] **Step 5: Day-Event-Modal-Flow durchspielen**

- Auf Dashboard-Seite navigieren.
- Klick auf einen normalen Tag-Balken → Modal „Tag markieren — DD.MM.YYYY".
- Quick-Pick „🎂 Geburtstag" wählen, Notiz „Test" eintippen, „Markieren" klicken.
- Modal schließt; nach kurzer Zeit sollte oben am Balken ein 🎂 erscheinen, Tooltip zeigt „🎂 — Test".
- Anomalie-Marker (rot) verschwindet, falls der Tag vorher als Anomalie galt.
- Erneut auf den Balken klicken → Modal zeigt vorausgewähltes 🎂 + Notiz.
- „Entfernen" klicken → Annotation weg.

- [ ] **Step 6: Massenimport-Tag im Modal prüfen**

- Auf Log-Seite einen Tag als Massenimport markieren (existing flow).
- Zurück zum Dashboard → der Balken ist grau.
- Klick auf den grauen Balken → Modal zeigt nur Hinweis „Dieser Tag ist als Massenimport markiert", kein Quick-Pick.

- [ ] **Step 7: Commit (falls keine Code-Änderungen während Smoke)**

```bash
# Falls im Smoke nichts kaputt war: kein Commit nötig.
# Falls Bugs: einzeln committen mit aussagekräftiger Message.
```

- [ ] **Step 8: Build-Container testen**

```bash
./build.sh all
```

Expected: beide Images bauen erfolgreich.

---

## Task 21: PROJECT_STATE.md aktualisieren

**Files:**
- Modify: `PROJECT_STATE.md`

- [ ] **Step 1: Änderungshistorie ergänzen**

Am Ende der Tabelle „Aenderungshistorie" (vor dem Datei-Ende) eine neue Zeile:

```
| 2026-04-25 | Coffee Status (GET /coffee/status), Power-Toggle in NavBar, Day-Event-Annotationen (MarkedDay mit kind+eventType) |
```

- [ ] **Step 2: API Endpoints Tabelle ergänzen**

In der Tabelle „API Endpoints" zwei neue Zeilen:

```
| GET    | `/coffee/status`              | Live-Status der Maschine (cached 7s) |
| POST   | `/coffee/power`               | Maschine ein-/ausschalten |
| GET    | `/api/stats/marked-days`      | Markierte Tage (mass-import + event), Filter ?kind= |
| POST   | `/api/stats/marked-days`      | Tag markieren |
| DELETE | `/api/stats/marked-days/{date}` | Markierung entfernen |
```

- [ ] **Step 3: Dashboard Features Tabelle ergänzen**

```
| Power-Toggle in NavBar (Status + Ein/Aus) | Done |
| Event-Annotationen auf Tagesbalken (🎂 👥 🎉 🏥 ✈️ 📌) | Done |
```

- [ ] **Step 4: Commit**

```bash
git add PROJECT_STATE.md
git commit -m "docs(state): coffee status, power toggle, day-event annotations done"
```

---

## Self-Review-Notiz (für ausführende Agent/Engineer)

Nach Abschluss aller Tasks:

1. **Spec-Coverage:** Jeder Punkt aus der Spec (`docs/superpowers/specs/2026-04-25-coffee-status-und-day-events-design.md`) ist durch eine Task abgedeckt:
   - Sektion 1 Datenmodell → Tasks 1, 4
   - Sektion 2 API-Endpoints → Tasks 3, 6, 7, 8
   - Sektion 3 Backend-Service → Tasks 7, 8
   - Sektion 4 NavBar Power-Button → Tasks 12, 13, 14
   - Sektion 5 Event-Annotationen → Tasks 15, 16, 17
   - Sektion 5 Anomaly-Filter → Task 17, 18
   - Sektion 6 Konfiguration → keine Code-Änderung an appsettings nötig (Task 7 nutzt bestehende `PowerWebhookUrl`)
   - Sektion 7 Tests → Tasks 5, 9
2. **Out-of-Scope:** dashboard-s7 — User passt selbst an, sobald Backend deployed ist.
3. **n8n-Workflow** muss um GET-Branch erweitert werden — out of scope für dieses Plan-Repo, aber ohne ihn liefert `/coffee/status` permanent `reachable:false` (die Frontend-States behandeln das korrekt).
