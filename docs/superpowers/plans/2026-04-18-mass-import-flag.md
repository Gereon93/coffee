# Mass-Import Flag Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In der LogPage kann ein Tag als "Massenimport" markiert werden (mit freiem Reason-Text). Markierte Tage fließen **nicht** in die KPI-Totals oder Anomaly-Detection ein und erscheinen im `DailyBarChart` als grauer Balken mit Label "Massenimport". Unmarkieren ist möglich.

**Architecture:** Neue Tabelle `ExcludedDays` (`Date` als PK, `Reason`, `CreatedAt`). Ein neuer Controller `ExcludedDaysController` bietet `GET/POST/DELETE /api/stats/excluded-days`. Die bestehenden Stats-Endpoints (`/api/stats/range`, `/api/stats/daily/*`) bleiben **unverändert** — das Frontend merged lokal. Frontend: neuer Hook `useExcludedDays`, der `DailyBarChart` + `KpiCardGrid` + `useAnomalyDetection` konsultieren. `LogPage` bekommt pro Snapshot-Zeile einen Button → Modal mit Reason-Eingabe → POST.

**Tech Stack:** ASP.NET Core Controller + EF migration, React 19 + TanStack Query (mit `useMutation`), Recharts (`<Cell>` Fill-Color für graue Bars), Tailwind CSS v4.

**Dependency:** **Plan 2 (EF Migrations Baseline) muss vorher merged und deployed sein**, sonst muss die neue Tabelle per Hand-ALTER angelegt werden — genau der Weg, den wir loswerden wollen.

---

## File Structure

| File | Responsibility |
|------|---------------|
| `CoffeeApi/Domain/ExcludedDay.cs` | Entity: `Date` (DateOnly, PK), `Reason`, `CreatedAt` |
| `CoffeeApi/Infrastructure/AppDbContext.cs:17` | DbSet + Entity-Config |
| `CoffeeApi/Migrations/<ts>_AddExcludedDays.cs` | Auto-gen EF migration |
| `CoffeeApi/DTOs/ExcludedDayDto.cs` | Request/Response DTOs |
| `CoffeeApi/Controllers/ExcludedDaysController.cs` | GET/POST/DELETE-Endpoints |
| `CoffeeTest/Controllers/ExcludedDaysControllerTests.cs` | Integration-Tests |
| `coffee-dashboard/src/api/types.ts` | TS-Types gespiegelt |
| `coffee-dashboard/src/api/stats.ts` | `fetchExcludedDays`, `addExcludedDay`, `removeExcludedDay` |
| `coffee-dashboard/src/hooks/useExcludedDays.ts` | Query + Mutations |
| `coffee-dashboard/src/lib/excludedDayUtils.ts` | `filterExcluded(days, excluded)`, `isExcluded(date, excluded)` |
| `coffee-dashboard/src/components/log/MarkAsBackfillModal.tsx` | Modal mit Reason-Eingabe |
| `coffee-dashboard/src/pages/LogPage.tsx` | Button pro Zeile + Status-Badge |
| `coffee-dashboard/src/components/charts/DailyBarChart.tsx` | Graue Cells + Label für excluded Days |
| `coffee-dashboard/src/components/cards/KpiCardGrid.tsx` | Excluded days in Summen überspringen |
| `coffee-dashboard/src/hooks/useAnomalyDetection.ts` | Excluded Days nicht in Z-Score einbeziehen |

---

## Prerequisites

- [ ] **Plan 2 ist merged + prod-deployed** (sonst abbrechen). Verify:
  ```bash
  sudo sqlite3 /volume2/docker_ssd/coffee-data/coffee.db "SELECT COUNT(*) FROM __EFMigrationsHistory;"
  ```
  Expected: ≥ 1.

- [ ] **Feature-Branch:**
  ```bash
  git checkout main && git pull
  git checkout -b feat/mass-import-flag
  ```

---

## Task 1: Domain-Entity `ExcludedDay`

**Files:**
- Create: `CoffeeApi/Domain/ExcludedDay.cs`

- [ ] **Step 1: Entity schreiben**

```csharp
namespace CoffeeApi.Domain;

/// <summary>
/// Marks a specific day as a mass-import / backfill day so it is
/// excluded from statistics aggregation and anomaly detection.
/// The underlying snapshots stay in the DB unchanged.
/// </summary>
public class ExcludedDay
{
    /// <summary>Local-date representation (yyyy-MM-dd). Primary key.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Free-text reason (e.g. "BSH API outage Feb 2026").</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Commit**

```bash
git add CoffeeApi/Domain/ExcludedDay.cs
git commit -m "feat(api): add ExcludedDay domain entity"
```

---

## Task 2: DbContext-Integration

**Files:**
- Modify: `CoffeeApi/Infrastructure/AppDbContext.cs`

- [ ] **Step 1: DbSet + Entity-Config ergänzen**

Ändere `CoffeeApi/Infrastructure/AppDbContext.cs`. Unter `public DbSet<MachineSnapshot> MachineSnapshots { get; set; }` hinzufügen:

```csharp
public DbSet<ExcludedDay> ExcludedDays { get; set; } = null!;
```

In `OnModelCreating`, nach dem `MachineSnapshot`-Block und vor der `utcConverter`-Schleife hinzufügen:

```csharp
modelBuilder.Entity<ExcludedDay>(entity =>
{
    entity.HasKey(e => e.Date);

    entity.Property(e => e.Date)
        .IsRequired()
        .HasConversion(
            v => v.ToString("yyyy-MM-dd"),
            v => DateOnly.Parse(v));

    entity.Property(e => e.Reason)
        .IsRequired()
        .HasMaxLength(500);

    entity.Property(e => e.CreatedAt)
        .IsRequired();
});
```

**Warum DateOnly → string?** SQLite hat keinen nativen Date-Typ und EF's DateOnly-Mapping ist in 9.0 für SQLite noch holperig. ISO-Strings (`yyyy-MM-dd`) sortieren korrekt und sind trivial zu parsen.

- [ ] **Step 2: Build verifizieren**

```bash
dotnet build Coffee.sln
```

Expected: 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add CoffeeApi/Infrastructure/AppDbContext.cs
git commit -m "feat(api): wire ExcludedDay into DbContext"
```

---

## Task 3: EF-Migration `AddExcludedDays`

**Files:**
- Create: `CoffeeApi/Migrations/<timestamp>_AddExcludedDays.cs` (auto-gen)

- [ ] **Step 1: Migration generieren**

```bash
cd CoffeeApi
dotnet ef migrations add AddExcludedDays
cd ..
```

Expected: neue `.cs`-Dateien unter `CoffeeApi/Migrations/`. Öffne die Migration — sie sollte nur `CREATE TABLE "ExcludedDays"` enthalten, **nichts** an `MachineSnapshots`. Wenn doch, ist OnModelCreating in Task 2 falsch geraten.

- [ ] **Step 2: Migration lokal gegen leere DB testen**

```bash
rm -f CoffeeApi/coffee.db
cd CoffeeApi
dotnet run &
PID=$!
sleep 5
curl -s http://localhost:5000/api/health > /dev/null && echo "API OK"
kill $PID
cd ..
sqlite3 CoffeeApi/coffee.db "SELECT name FROM sqlite_master WHERE type='table';"
```

Expected: `MachineSnapshots`, `ExcludedDays`, `__EFMigrationsHistory`.

- [ ] **Step 3: Commit**

```bash
git add CoffeeApi/Migrations
git commit -m "feat(db): migration AddExcludedDays"
```

---

## Task 4: DTOs

**Files:**
- Create: `CoffeeApi/DTOs/ExcludedDayDto.cs`

- [ ] **Step 1: DTOs schreiben**

```csharp
namespace CoffeeApi.DTOs;

public class ExcludedDayDto
{
    /// <summary>Local date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class CreateExcludedDayDto
{
    /// <summary>Local date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Commit**

```bash
git add CoffeeApi/DTOs/ExcludedDayDto.cs
git commit -m "feat(api): add ExcludedDay DTOs"
```

---

## Task 5: Controller — GET-Endpoint (TDD)

**Files:**
- Create: `CoffeeApi/Controllers/ExcludedDaysController.cs`
- Create: `CoffeeTest/Controllers/ExcludedDaysControllerTests.cs`

- [ ] **Step 1: Test: GET liefert leere Liste bei leerer DB**

```csharp
// CoffeeTest/Controllers/ExcludedDaysControllerTests.cs
using CoffeeApi.Controllers;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeTest.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Controllers;

public class ExcludedDaysControllerTests
{
    private static ExcludedDaysController CreateController(AppDbContext db)
    {
        return new ExcludedDaysController(db, NullLogger<ExcludedDaysController>.Instance);
    }

    [Fact]
    public async Task GetAll_EmptyDb_ReturnsEmptyList()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ExcludedDayDto>>(ok.Value);
        Assert.Empty(list);
    }
}
```

- [ ] **Step 2: Test ausführen, muss rot sein**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~ExcludedDaysControllerTests
```

Expected: FAIL — `ExcludedDaysController` existiert nicht.

- [ ] **Step 3: Controller mit GET**

```csharp
// CoffeeApi/Controllers/ExcludedDaysController.cs
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Controllers;

[ApiController]
[Route("api/stats/excluded-days")]
public class ExcludedDaysController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ExcludedDaysController> _logger;

    public ExcludedDaysController(AppDbContext context, ILogger<ExcludedDaysController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExcludedDayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var days = await _context.ExcludedDays
            .OrderByDescending(d => d.Date)
            .Select(d => new ExcludedDayDto
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                Reason = d.Reason,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return Ok(days);
    }
}
```

- [ ] **Step 4: Test muss jetzt grün**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~ExcludedDaysControllerTests
```

Expected: 1/1 PASS.

- [ ] **Step 5: Commit**

```bash
git add CoffeeApi/Controllers/ExcludedDaysController.cs CoffeeTest/Controllers/ExcludedDaysControllerTests.cs
git commit -m "feat(api): GET /api/stats/excluded-days"
```

---

## Task 6: POST-Endpoint (TDD)

**Files:**
- Modify: `CoffeeApi/Controllers/ExcludedDaysController.cs`
- Modify: `CoffeeTest/Controllers/ExcludedDaysControllerTests.cs`

- [ ] **Step 1: Tests ergänzen**

Füge nach dem bestehenden `GetAll_EmptyDb...` folgendes in `ExcludedDaysControllerTests.cs` an:

```csharp
[Fact]
public async Task Create_ValidPayload_Returns201AndPersists()
{
    using var db = TestDbContextFactory.Create();
    var controller = CreateController(db);
    var dto = new CreateExcludedDayDto { Date = "2026-02-15", Reason = "BSH API outage" };

    var result = await controller.Create(dto);

    var created = Assert.IsType<CreatedAtActionResult>(result);
    var returned = Assert.IsType<ExcludedDayDto>(created.Value);
    Assert.Equal("2026-02-15", returned.Date);
    Assert.Equal("BSH API outage", returned.Reason);

    var row = Assert.Single(await db.ExcludedDays.ToListAsync());
    Assert.Equal(new DateOnly(2026, 2, 15), row.Date);
}

[Fact]
public async Task Create_InvalidDate_Returns400()
{
    using var db = TestDbContextFactory.Create();
    var controller = CreateController(db);
    var dto = new CreateExcludedDayDto { Date = "not-a-date", Reason = "x" };

    var result = await controller.Create(dto);

    Assert.IsType<BadRequestObjectResult>(result);
}

[Fact]
public async Task Create_EmptyReason_Returns400()
{
    using var db = TestDbContextFactory.Create();
    var controller = CreateController(db);
    var dto = new CreateExcludedDayDto { Date = "2026-02-15", Reason = "" };

    var result = await controller.Create(dto);

    Assert.IsType<BadRequestObjectResult>(result);
}

[Fact]
public async Task Create_ExistingDate_Returns409()
{
    using var db = TestDbContextFactory.Create();
    db.ExcludedDays.Add(new ExcludedDay { Date = new DateOnly(2026, 2, 15), Reason = "first", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();
    var controller = CreateController(db);
    var dto = new CreateExcludedDayDto { Date = "2026-02-15", Reason = "second" };

    var result = await controller.Create(dto);

    Assert.IsType<ConflictObjectResult>(result);
}
```

- [ ] **Step 2: Tests rot**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~ExcludedDaysControllerTests
```

Expected: 4 FAIL (`Create` nicht vorhanden).

- [ ] **Step 3: POST implementieren**

Füge in `ExcludedDaysController` hinzu (vor der Klassen-Schließklammer):

```csharp
[HttpPost]
[ProducesResponseType(typeof(ExcludedDayDto), StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<IActionResult> Create([FromBody] CreateExcludedDayDto dto)
{
    if (!DateOnly.TryParseExact(dto.Date, "yyyy-MM-dd", out var parsedDate))
    {
        return BadRequest(new { error = "Invalid date", details = new[] { "Use yyyy-MM-dd format" } });
    }

    if (string.IsNullOrWhiteSpace(dto.Reason))
    {
        return BadRequest(new { error = "Reason required", details = new[] { "Reason must not be empty" } });
    }

    var exists = await _context.ExcludedDays.AnyAsync(d => d.Date == parsedDate);
    if (exists)
    {
        return Conflict(new { error = "Already excluded", details = new[] { $"Day {dto.Date} is already marked as excluded" } });
    }

    var entity = new ExcludedDay
    {
        Date = parsedDate,
        Reason = dto.Reason.Trim(),
        CreatedAt = DateTime.UtcNow
    };
    _context.ExcludedDays.Add(entity);
    await _context.SaveChangesAsync();

    _logger.LogInformation("Day {Date} marked as excluded: {Reason}", dto.Date, entity.Reason);

    var response = new ExcludedDayDto
    {
        Date = entity.Date.ToString("yyyy-MM-dd"),
        Reason = entity.Reason,
        CreatedAt = entity.CreatedAt
    };

    return CreatedAtAction(nameof(GetAll), response);
}
```

- [ ] **Step 4: Tests grün**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~ExcludedDaysControllerTests
```

Expected: 5/5 PASS.

- [ ] **Step 5: Commit**

```bash
git add CoffeeApi/Controllers/ExcludedDaysController.cs CoffeeTest/Controllers/ExcludedDaysControllerTests.cs
git commit -m "feat(api): POST /api/stats/excluded-days with validation"
```

---

## Task 7: DELETE-Endpoint (TDD)

**Files:**
- Modify: `CoffeeApi/Controllers/ExcludedDaysController.cs`
- Modify: `CoffeeTest/Controllers/ExcludedDaysControllerTests.cs`

- [ ] **Step 1: Tests ergänzen**

```csharp
[Fact]
public async Task Delete_Existing_Returns204AndRemoves()
{
    using var db = TestDbContextFactory.Create();
    db.ExcludedDays.Add(new ExcludedDay { Date = new DateOnly(2026, 2, 15), Reason = "x", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();
    var controller = CreateController(db);

    var result = await controller.Delete("2026-02-15");

    Assert.IsType<NoContentResult>(result);
    Assert.Empty(await db.ExcludedDays.ToListAsync());
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
```

- [ ] **Step 2: Tests rot**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~ExcludedDaysControllerTests
```

Expected: 3 FAIL (`Delete` nicht vorhanden).

- [ ] **Step 3: DELETE implementieren**

Füge in `ExcludedDaysController` an:

```csharp
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

    var entity = await _context.ExcludedDays.FirstOrDefaultAsync(d => d.Date == parsedDate);
    if (entity == null)
    {
        return NotFound(new { error = "Not found", details = new[] { $"Day {date} is not marked as excluded" } });
    }

    _context.ExcludedDays.Remove(entity);
    await _context.SaveChangesAsync();

    _logger.LogInformation("Day {Date} removed from excluded list", date);
    return NoContent();
}
```

- [ ] **Step 4: Tests grün**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj --filter FullyQualifiedName~ExcludedDaysControllerTests
```

Expected: 8/8 PASS (alle Excluded-Tests).

- [ ] **Step 5: Gesamt-Testsuite**

```bash
dotnet test
```

Expected: 41 Tests grün (33 bestehende + 8 neue).

- [ ] **Step 6: Commit**

```bash
git add CoffeeApi/Controllers/ExcludedDaysController.cs CoffeeTest/Controllers/ExcludedDaysControllerTests.cs
git commit -m "feat(api): DELETE /api/stats/excluded-days/{date}"
```

---

## Task 8: Frontend — Types

**Files:**
- Modify: `coffee-dashboard/src/api/types.ts`

- [ ] **Step 1: Types hinzufügen**

Am Ende von `types.ts` (nach `HealthResponse`) anfügen:

```typescript
export interface ExcludedDay {
  date: string;        // yyyy-MM-dd
  reason: string;
  createdAt: string;   // ISO timestamp
}

export interface CreateExcludedDayPayload {
  date: string;        // yyyy-MM-dd
  reason: string;
}
```

- [ ] **Step 2: Commit**

```bash
git add coffee-dashboard/src/api/types.ts
git commit -m "feat(dashboard): add ExcludedDay types"
```

---

## Task 9: Frontend — API-Client

**Files:**
- Modify: `coffee-dashboard/src/api/stats.ts`

- [ ] **Step 1: Funktionen anhängen**

Am Ende von `stats.ts`:

```typescript
import type { ExcludedDay, CreateExcludedDayPayload } from './types';

export function fetchExcludedDays() {
  return fetchJson<ExcludedDay[]>('/api/stats/excluded-days');
}

export async function addExcludedDay(payload: CreateExcludedDayPayload): Promise<ExcludedDay> {
  const res = await fetch('/api/stats/excluded-days', {
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

export async function removeExcludedDay(date: string): Promise<void> {
  const res = await fetch(`/api/stats/excluded-days/${date}`, { method: 'DELETE' });
  if (!res.ok && res.status !== 204) {
    const body = await res.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(body.error ?? `HTTP ${res.status}`);
  }
}
```

**Hinweis:** Anstatt `fetchJson` nutze hier direkt `fetch`, weil wir Error-Bodies strukturiert auslesen wollen und DELETE kein JSON zurückgibt (204 No Content).

- [ ] **Step 2: Commit**

```bash
git add coffee-dashboard/src/api/stats.ts
git commit -m "feat(dashboard): add excluded-days API client functions"
```

---

## Task 10: Frontend — Hook `useExcludedDays`

**Files:**
- Create: `coffee-dashboard/src/hooks/useExcludedDays.ts`

- [ ] **Step 1: Hook schreiben**

```typescript
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchExcludedDays,
  addExcludedDay,
  removeExcludedDay,
} from '../api/stats';
import type { CreateExcludedDayPayload } from '../api/types';

const QUERY_KEY = ['excluded-days'] as const;

export function useExcludedDays() {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: fetchExcludedDays,
    staleTime: 60_000,
  });
}

export function useAddExcludedDay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateExcludedDayPayload) => addExcludedDay(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEY });
      qc.invalidateQueries({ queryKey: ['range'] });
      qc.invalidateQueries({ queryKey: ['daily'] });
    },
  });
}

export function useRemoveExcludedDay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (date: string) => removeExcludedDay(date),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEY });
      qc.invalidateQueries({ queryKey: ['range'] });
      qc.invalidateQueries({ queryKey: ['daily'] });
    },
  });
}
```

**Invalidierung:** Mutation muss `range` + `daily` invalidieren, damit das Dashboard nach Markieren sofort ohne den Tag rechnet.

- [ ] **Step 2: Commit**

```bash
git add coffee-dashboard/src/hooks/useExcludedDays.ts
git commit -m "feat(dashboard): useExcludedDays query + mutation hooks"
```

---

## Task 11: Frontend — Filter-Utility

**Files:**
- Create: `coffee-dashboard/src/lib/excludedDayUtils.ts`

- [ ] **Step 1: Utility schreiben**

```typescript
import type { DailyAggregate, ExcludedDay } from '../api/types';

export function buildExcludedSet(excluded: ExcludedDay[] | undefined): Set<string> {
  return new Set((excluded ?? []).map((d) => d.date));
}

export function isExcluded(
  date: string,
  excludedSet: Set<string>,
): boolean {
  return excludedSet.has(date);
}

export function filterNonExcluded(
  days: DailyAggregate[] | undefined,
  excludedSet: Set<string>,
): DailyAggregate[] {
  return (days ?? []).filter((d) => !excludedSet.has(d.date));
}
```

- [ ] **Step 2: Commit**

```bash
git add coffee-dashboard/src/lib/excludedDayUtils.ts
git commit -m "feat(dashboard): excluded-day filter utilities"
```

---

## Task 12: Frontend — `MarkAsBackfillModal` Komponente

**Files:**
- Create: `coffee-dashboard/src/components/log/MarkAsBackfillModal.tsx`

- [ ] **Step 1: Modal schreiben**

```tsx
import { useState, useEffect } from 'react';
import { X } from 'lucide-react';
import { useAddExcludedDay } from '../../hooks/useExcludedDays';

interface Props {
  date: string; // yyyy-MM-dd
  displayDate: string; // e.g. "15.02.2026"
  open: boolean;
  onClose: () => void;
}

export function MarkAsBackfillModal({ date, displayDate, open, onClose }: Props) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const mutation = useAddExcludedDay();

  useEffect(() => {
    if (open) {
      setReason('');
      setError(null);
    }
  }, [open]);

  if (!open) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setError('Bitte gib einen Grund an.');
      return;
    }
    try {
      await mutation.mutateAsync({ date, reason: reason.trim() });
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
          <h2 className="text-lg font-semibold">Als Massenimport markieren</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-stone-400 hover:bg-stone-100 hover:text-stone-600 dark:hover:bg-stone-800"
            aria-label="Schliessen"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <p className="mb-4 text-sm text-stone-600 dark:text-stone-400">
          Der Tag <strong>{displayDate}</strong> wird aus Tages-, Wochen- und
          Monats-Aggregaten ausgeblendet und als grauer Balken dargestellt.
        </p>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="reason" className="mb-1 block text-sm font-medium">
              Grund
            </label>
            <input
              id="reason"
              type="text"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="z.B. BSH API Ausfall, Initialimport ..."
              className="w-full rounded-md border border-stone-300 bg-white px-3 py-2 text-sm focus:border-coffee-500 focus:outline-none focus:ring-1 focus:ring-coffee-500 dark:border-stone-700 dark:bg-stone-800"
              autoFocus
              maxLength={500}
            />
          </div>

          {error && (
            <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
          )}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-stone-300 px-3 py-1.5 text-sm font-medium hover:bg-stone-100 dark:border-stone-700 dark:hover:bg-stone-800"
              disabled={mutation.isPending}
            >
              Abbrechen
            </button>
            <button
              type="submit"
              disabled={mutation.isPending || !reason.trim()}
              className="rounded-md bg-coffee-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-coffee-700 disabled:opacity-50"
            >
              {mutation.isPending ? 'Speichere...' : 'Markieren'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Build check**

```bash
cd coffee-dashboard
npx tsc --noEmit
cd ..
```

Expected: 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add coffee-dashboard/src/components/log/MarkAsBackfillModal.tsx
git commit -m "feat(dashboard): MarkAsBackfillModal with reason input"
```

---

## Task 13: Frontend — LogPage Integration

**Files:**
- Modify: `coffee-dashboard/src/pages/LogPage.tsx`

- [ ] **Step 1: LogPage umbauen**

Ersetze die komplette `LogPage.tsx`:

```tsx
import { useState } from 'react';
import { ChevronLeft, ChevronRight, AlertCircle, Undo2 } from 'lucide-react';
import { useSnapshots } from '../hooks/useSnapshots';
import { useExcludedDays, useRemoveExcludedDay } from '../hooks/useExcludedDays';
import { LoadingSpinner } from '../components/shared/LoadingSpinner';
import { ErrorMessage } from '../components/shared/ErrorMessage';
import { MarkAsBackfillModal } from '../components/log/MarkAsBackfillModal';
import { buildExcludedSet } from '../lib/excludedDayUtils';
import type { SnapshotResponse } from '../api/types';

function formatLocalTime(isoTimestamp: string): string {
  return new Date(isoTimestamp).toLocaleString('de-DE', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  });
}

function toLocalDateKey(isoTimestamp: string): string {
  // yyyy-MM-dd in local timezone (matches how backend stores excluded days)
  const d = new Date(isoTimestamp);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function formatDisplayDate(dateKey: string): string {
  const [y, m, d] = dateKey.split('-');
  return `${d}.${m}.${y}`;
}

function DeltaBadge({
  current, previous, field,
}: {
  current: SnapshotResponse;
  previous: SnapshotResponse | null;
  field: keyof Pick<SnapshotResponse, 'beverageCounterCoffee' | 'beverageCounterCoffeeAndMilk' | 'beverageCounterMilk' | 'beverageCounterHotWaterCups'>;
}) {
  if (!previous) return null;
  const delta = current[field] - previous[field];
  if (delta <= 0) return null;
  return (
    <span className="ml-1 inline-block rounded bg-amber-100 px-1 text-xs font-semibold text-amber-800 dark:bg-amber-900 dark:text-amber-200">
      +{delta}
    </span>
  );
}

export function LogPage() {
  const [page, setPage] = useState(1);
  const [modalDateKey, setModalDateKey] = useState<string | null>(null);
  const pageSize = 25;

  const { data, isLoading, isError } = useSnapshots(page, pageSize);
  const excluded = useExcludedDays();
  const removeMutation = useRemoveExcludedDay();

  if (isLoading) return <LoadingSpinner />;
  if (isError) return <ErrorMessage />;
  if (!data) return null;

  const { data: snapshots, pagination } = data;
  const excludedSet = buildExcludedSet(excluded.data);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Snapshot Log</h1>
        <span className="text-sm text-stone-500 dark:text-stone-400">
          {pagination.totalItems} Snapshots
        </span>
      </div>

      <div className="overflow-x-auto rounded-xl border border-stone-200 bg-white shadow-sm dark:border-stone-800 dark:bg-stone-900">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-stone-200 bg-stone-50 dark:border-stone-800 dark:bg-stone-950">
              <th className="px-3 py-2.5 font-semibold">ID</th>
              <th className="px-3 py-2.5 font-semibold">Zeitpunkt (Lokal)</th>
              <th className="px-3 py-2.5 font-semibold text-right">Kaffee</th>
              <th className="px-3 py-2.5 font-semibold text-right">Kaffee+Milch</th>
              <th className="px-3 py-2.5 font-semibold text-right">Milch</th>
              <th className="px-3 py-2.5 font-semibold text-right">Heisswasser</th>
              <th className="px-3 py-2.5 font-semibold text-right">Total</th>
              <th className="px-3 py-2.5 font-semibold">Status</th>
              <th className="px-3 py-2.5 font-semibold">Tag</th>
            </tr>
          </thead>
          <tbody>
            {snapshots.map((s: SnapshotResponse, i: number) => {
              const prev = i < snapshots.length - 1 ? snapshots[i + 1] : null;
              const dateKey = toLocalDateKey(s.timestamp);
              const isDayExcluded = excludedSet.has(dateKey);

              return (
                <tr
                  key={s.id}
                  className={`border-b border-stone-100 transition-colors hover:bg-stone-50 dark:border-stone-800/50 dark:hover:bg-stone-800/30 ${
                    isDayExcluded ? 'bg-stone-100/50 dark:bg-stone-800/20' : ''
                  }`}
                >
                  <td className="px-3 py-2 font-mono text-xs text-stone-500">{s.id}</td>
                  <td className="px-3 py-2 whitespace-nowrap">{formatLocalTime(s.timestamp)}</td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {s.beverageCounterCoffee}
                    <DeltaBadge current={s} previous={prev} field="beverageCounterCoffee" />
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {s.beverageCounterCoffeeAndMilk}
                    <DeltaBadge current={s} previous={prev} field="beverageCounterCoffeeAndMilk" />
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {s.beverageCounterMilk}
                    <DeltaBadge current={s} previous={prev} field="beverageCounterMilk" />
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {s.beverageCounterHotWaterCups}
                    <DeltaBadge current={s} previous={prev} field="beverageCounterHotWaterCups" />
                  </td>
                  <td className="px-3 py-2 text-right font-semibold tabular-nums">{s.totalBeverages}</td>
                  <td className="px-3 py-2">
                    <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${
                      s.operationState === 'Ready'
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900 dark:text-emerald-300'
                        : 'bg-stone-100 text-stone-600 dark:bg-stone-800 dark:text-stone-400'
                    }`}>
                      {s.operationState}
                    </span>
                  </td>
                  <td className="px-3 py-2">
                    {isDayExcluded ? (
                      <button
                        type="button"
                        onClick={() => removeMutation.mutate(dateKey)}
                        disabled={removeMutation.isPending}
                        className="inline-flex items-center gap-1 rounded-full bg-stone-200 px-2 py-0.5 text-xs font-medium text-stone-700 hover:bg-stone-300 dark:bg-stone-700 dark:text-stone-300 dark:hover:bg-stone-600"
                        title="Markierung entfernen"
                      >
                        <Undo2 className="h-3 w-3" /> Massenimport
                      </button>
                    ) : (
                      <button
                        type="button"
                        onClick={() => setModalDateKey(dateKey)}
                        className="inline-flex items-center gap-1 rounded-full border border-stone-200 px-2 py-0.5 text-xs font-medium text-stone-500 hover:bg-stone-100 dark:border-stone-700 dark:text-stone-400 dark:hover:bg-stone-800"
                        title="Tag als Massenimport markieren"
                      >
                        <AlertCircle className="h-3 w-3" /> markieren
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between">
        <span className="text-sm text-stone-500 dark:text-stone-400">
          Seite {pagination.page} von {pagination.totalPages}
        </span>
        <div className="flex gap-2">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="flex items-center gap-1 rounded-lg border border-stone-200 px-3 py-1.5 text-sm font-medium transition-colors hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed dark:border-stone-700 dark:hover:bg-stone-800"
          >
            <ChevronLeft className="h-4 w-4" /> Zurueck
          </button>
          <button
            onClick={() => setPage((p) => Math.min(pagination.totalPages, p + 1))}
            disabled={page >= pagination.totalPages}
            className="flex items-center gap-1 rounded-lg border border-stone-200 px-3 py-1.5 text-sm font-medium transition-colors hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed dark:border-stone-700 dark:hover:bg-stone-800"
          >
            Weiter <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      </div>

      {modalDateKey && (
        <MarkAsBackfillModal
          date={modalDateKey}
          displayDate={formatDisplayDate(modalDateKey)}
          open={true}
          onClose={() => setModalDateKey(null)}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 2: TypeScript-Check**

```bash
cd coffee-dashboard
npx tsc --noEmit
cd ..
```

Expected: 0 Errors.

- [ ] **Step 3: Manuell testen**

```bash
cd coffee-dashboard
npm run dev &
DEV_PID=$!
# Warte kurz, dann öffne http://localhost:5173/log
```

Klicke "markieren" auf einer Zeile → Modal öffnet → Reason eingeben → Submit → Badge wechselt zu grauem "Massenimport". Klick darauf → Markierung verschwindet wieder.

```bash
kill $DEV_PID
```

- [ ] **Step 4: Commit**

```bash
git add coffee-dashboard/src/pages/LogPage.tsx
git commit -m "feat(dashboard): log page action to mark/unmark day as backfill"
```

---

## Task 14: `DailyBarChart` — graue Balken für excluded

**Files:**
- Modify: `coffee-dashboard/src/components/charts/DailyBarChart.tsx`

- [ ] **Step 1: Chart updaten**

Ersetze `DailyBarChart.tsx`:

```tsx
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell,
} from 'recharts';
import type { DailyAggregate } from '../../api/types';
import type { AnomalyResult } from '../../lib/anomalyUtils';
import { formatDate } from '../../lib/dateUtils';

interface Props {
  data: DailyAggregate[];
  anomalies: AnomalyResult[];
  excludedSet: Set<string>;
}

export function DailyBarChart({ data, anomalies, excludedSet }: Props) {
  const anomalyDates = new Set(
    anomalies.filter((a) => a.isAnomaly).map((a) => a.date),
  );

  const chartData = data.map((d) => ({
    ...d,
    label: formatDate(d.date),
    isAnomaly: anomalyDates.has(d.date),
    isExcluded: excludedSet.has(d.date),
  }));

  const getCoffeeFill = (entry: typeof chartData[number]) => {
    if (entry.isExcluded) return '#a8a29e'; // stone-400 (grey)
    if (entry.isAnomaly) return '#ef4444';
    return '#d97706';
  };

  const getMilkFill = (entry: typeof chartData[number]) => {
    if (entry.isExcluded) return '#d6d3d1'; // stone-300 (lighter grey)
    if (entry.isAnomaly) return '#fca5a5';
    return '#3b82f6';
  };

  const getStroke = (entry: typeof chartData[number]) => {
    if (entry.isExcluded) return '#78716c'; // stone-500
    if (entry.isAnomaly) return '#dc2626';
    return 'none';
  };

  return (
    <div className="rounded-xl border border-stone-200 bg-white p-5 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <h3 className="mb-4 text-sm font-semibold text-stone-700 dark:text-stone-300">
        Taeglicher Verbrauch
      </h3>
      <ResponsiveContainer width="100%" height={300}>
        <BarChart data={chartData}>
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
            formatter={(value?: number | string, name?: string, item?: { payload?: typeof chartData[number] }) => {
              const label = name === 'coffeeCount' ? 'Kaffee' : 'Milch';
              if (item?.payload?.isExcluded) {
                return [`${value ?? 0} (Massenimport)`, label];
              }
              return [value ?? 0, label];
            }}
          />
          <Bar dataKey="coffeeCount" stackId="a" radius={[0, 0, 0, 0]}>
            {chartData.map((entry, i) => (
              <Cell
                key={i}
                fill={getCoffeeFill(entry)}
                stroke={getStroke(entry)}
                strokeWidth={entry.isAnomaly || entry.isExcluded ? 2 : 0}
              />
            ))}
          </Bar>
          <Bar dataKey="milkCount" stackId="a" radius={[4, 4, 0, 0]}>
            {chartData.map((entry, i) => (
              <Cell
                key={i}
                fill={getMilkFill(entry)}
                stroke={getStroke(entry)}
                strokeWidth={entry.isAnomaly || entry.isExcluded ? 2 : 0}
              />
            ))}
          </Bar>
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
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add coffee-dashboard/src/components/charts/DailyBarChart.tsx
git commit -m "feat(dashboard): grey bars with 'Massenimport' label for excluded days"
```

---

## Task 15: Anomaly-Detection ignoriert excluded Days

**Files:**
- Modify: `coffee-dashboard/src/hooks/useAnomalyDetection.ts`

- [ ] **Step 1: Hook signature erweitern**

```typescript
import { useMemo } from 'react';
import type { DailyAggregate } from '../api/types';
import { detectAnomalies } from '../lib/anomalyUtils';

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

**Warum vorher filtern, nicht nachher?** Ein 30-Kaffee-Tag würde Mean und StdDev verzerren und andere Tage fälschlich als "normal" erscheinen lassen. Excluded Days müssen aus der Statistikgrundlage raus, bevor Z-Scores berechnet werden.

- [ ] **Step 2: Commit**

```bash
git add coffee-dashboard/src/hooks/useAnomalyDetection.ts
git commit -m "feat(dashboard): exclude marked days from anomaly z-score basis"
```

---

## Task 16: `KpiCardGrid` filtert excluded aus Totals

**Files:**
- Modify: `coffee-dashboard/src/components/cards/KpiCardGrid.tsx`

- [ ] **Step 1: Signature + Aggregation anpassen**

Ersetze `KpiCardGrid.tsx`:

```tsx
import { Coffee, Milk, GlassWater } from 'lucide-react';
import { KpiCard } from './KpiCard';
import type { DailySummary, DailyAggregate, SnapshotResponse } from '../../api/types';
import type { TimePeriod } from '../../lib/dateUtils';

interface Props {
  summary: DailySummary | undefined;
  rangeData?: DailyAggregate[];
  excludedSet: Set<string>;
  period: TimePeriod;
  latestSnapshot?: SnapshotResponse | null;
}

const periodLabels: Record<TimePeriod, string> = {
  week: 'Diese Woche',
  month: 'Dieser Monat',
  year: 'Dieses Jahr',
  all: 'Gesamt',
};

export function KpiCardGrid({ summary, rangeData, excludedSet, period, latestSnapshot }: Props) {
  const s = summary ?? { totalToday: 0, coffeeToday: 0, milkDrinksToday: 0, peakHour: null };
  const label = periodLabels[period];

  const isAll = period === 'all' && latestSnapshot;
  const filteredRange = (rangeData ?? []).filter((d) => !excludedSet.has(d.date));

  const rangeCoffee = isAll
    ? latestSnapshot.beverageCounterCoffee
    : filteredRange.reduce((sum, d) => sum + d.coffeeCount, 0);

  const rangeMilk = isAll
    ? latestSnapshot.beverageCounterCoffeeAndMilk + latestSnapshot.beverageCounterMilk
    : filteredRange.reduce((sum, d) => sum + d.milkCount, 0);

  const rangeTotal = isAll
    ? latestSnapshot.totalBeverages
    : filteredRange.reduce((sum, d) => sum + d.total, 0);

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <KpiCard
        title="Heute"
        value={s.totalToday}
        icon={GlassWater}
        subtitle={`Kaffee: ${s.coffeeToday} · Milch: ${s.milkDrinksToday}`}
      />
      <KpiCard
        title={label}
        value={rangeTotal}
        icon={Coffee}
        subtitle="Alle Bezuege"
      />
      <KpiCard
        title="Kaffee"
        value={rangeCoffee}
        icon={Coffee}
        subtitle={label}
      />
      <KpiCard
        title="Milch"
        value={rangeMilk}
        icon={Milk}
        subtitle={label}
      />
    </div>
  );
}
```

**Hinweis:** "Gesamt" (`period === 'all'`) nutzt weiterhin `latestSnapshot`-Counter, also absolute Zählerstände der Maschine. Das ist korrekt: excluded Days ändern keine Zählerwerte, nur deren Zuordnung zu einem Bucket.

- [ ] **Step 2: Commit**

```bash
git add coffee-dashboard/src/components/cards/KpiCardGrid.tsx
git commit -m "feat(dashboard): KPI totals skip excluded days"
```

---

## Task 17: `DashboardPage` verdrahtet alles

**Files:**
- Modify: `coffee-dashboard/src/pages/DashboardPage.tsx`

- [ ] **Step 1: Hook-Aufrufe ergänzen und props weiterreichen**

Ersetze `DashboardPage.tsx`:

```tsx
import { KpiCardGrid } from '../components/cards/KpiCardGrid';
import { TimePeriodSelector } from '../components/controls/TimePeriodSelector';
import { DailyBarChart } from '../components/charts/DailyBarChart';
import { TrendLineChart } from '../components/charts/TrendLineChart';
import { ConsumptionPieChart } from '../components/charts/ConsumptionPieChart';
import { HourlyPeaksChart } from '../components/charts/HourlyPeaksChart';
import { WeekdayComparisonChart } from '../components/charts/WeekdayComparisonChart';
import { LoadingSpinner } from '../components/shared/LoadingSpinner';
import { ErrorMessage } from '../components/shared/ErrorMessage';
import { useDailyStats } from '../hooks/useDailyStats';
import { useStatsRange } from '../hooks/useStatsRange';
import { useHeatmap } from '../hooks/useHeatmap';
import { useLatestSnapshot } from '../hooks/useLatestSnapshot';
import { useTimePeriod } from '../hooks/useTimePeriod';
import { useAnomalyDetection } from '../hooks/useAnomalyDetection';
import { useExcludedDays } from '../hooks/useExcludedDays';
import { buildExcludedSet } from '../lib/excludedDayUtils';

export function DashboardPage() {
  const { period, setPeriod, from, to } = useTimePeriod();
  const daily = useDailyStats();
  const range = useStatsRange(from, to);
  const heatmap = useHeatmap(4);
  const latest = useLatestSnapshot();
  const excluded = useExcludedDays();
  const excludedSet = buildExcludedSet(excluded.data);
  const anomalies = useAnomalyDetection(range.data?.data, excludedSet);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-bold">Dashboard</h1>
        <TimePeriodSelector value={period} onChange={setPeriod} />
      </div>

      {daily.isLoading ? (
        <LoadingSpinner />
      ) : daily.isError ? (
        <ErrorMessage />
      ) : (
        <>
          <KpiCardGrid
            summary={daily.data?.summary}
            rangeData={range.data?.data}
            excludedSet={excludedSet}
            period={period}
            latestSnapshot={latest.data}
          />
          {daily.data?.snapshots && (
            <HourlyPeaksChart snapshots={daily.data.snapshots} />
          )}
        </>
      )}

      {range.isLoading ? (
        <LoadingSpinner />
      ) : range.isError ? (
        <ErrorMessage />
      ) : range.data ? (
        <>
          <DailyBarChart data={range.data.data} anomalies={anomalies} excludedSet={excludedSet} />

          <div className="grid gap-6 lg:grid-cols-2">
            <TrendLineChart data={range.data.data} />
            <ConsumptionPieChart data={range.data.data} />
          </div>

          <div className="grid gap-6 lg:grid-cols-2">
            {heatmap.data?.heatmap && (
              <WeekdayComparisonChart heatmap={heatmap.data.heatmap} />
            )}
          </div>
        </>
      ) : null}
    </div>
  );
}
```

**Hinweis:** `TrendLineChart` und `ConsumptionPieChart` nutzen weiterhin `range.data.data` ungefiltert. Willst du die auch filtern? Das würde Konsistenz bringen, aber ggf. eine visuelle Lücke im Trend erzeugen. Kompromiss: lass sie vorerst — wenn im End-to-End-Test (Task 18) offensichtlich daneben, filter im nächsten Commit.

- [ ] **Step 2: TypeScript-Check**

```bash
cd coffee-dashboard
npx tsc --noEmit
cd ..
```

Expected: 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add coffee-dashboard/src/pages/DashboardPage.tsx
git commit -m "feat(dashboard): wire excluded-days into dashboard aggregation"
```

---

## Task 18: End-to-End Smoke Test (lokal)

**Files:** keine Änderungen — nur Verifikation.

- [ ] **Step 1: Backend starten**

```bash
cd CoffeeApi
rm -f coffee.db   # saubere DB
dotnet run &
API_PID=$!
cd ..
sleep 5
```

- [ ] **Step 2: Frontend starten**

```bash
cd coffee-dashboard
npm run dev &
UI_PID=$!
cd ..
```

- [ ] **Step 3: Testdaten einspeisen**

Erzeuge ein paar Snapshots verteilt auf mehrere Tage, mit einem expliziten "Ausreißer"-Tag. Ein simples Skript:

```bash
for DAY in 1 2 3 4 5; do
  DATE=$(date -u -v-${DAY}d +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -d "$DAY days ago" +%Y-%m-%dT%H:%M:%SZ)
  COFFEE_COUNT=$(( DAY * 3 + ($DAY == 3 ? 40 : 0) ))  # Tag 3 = Ausreisser
  curl -sS -X POST http://localhost:5000/api/ingest \
    -H "Content-Type: application/json" \
    -d "{\"data\":{\"status\":[{\"key\":\"ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee\",\"value\":$COFFEE_COUNT}]}}" \
    > /dev/null
  sleep 1
done

curl -sS http://localhost:5000/api/stats | head -c 500
```

- [ ] **Step 4: Browser-Test**

Öffne `http://localhost:5173` — Dashboard sollte Daten zeigen mit Tag 3 rot markiert (Anomalie).

- [ ] **Step 5: Log-Tab → markieren**

Gehe zu `/log`, klicke bei einer Zeile aus Tag 3 auf "markieren", gib "Testausreißer" ein, submit.

Expected:
- Badge wechselt sofort zu grauem "Massenimport".
- Zurück auf `/` (Dashboard): Tag 3 erscheint nun **grau** im Balkendiagramm (nicht rot).
- KPI-Karte "Diese Woche" ist niedriger (40 Kaffees weniger).

- [ ] **Step 6: Markierung entfernen**

Im Log-Tab auf "Massenimport"-Badge klicken → Markierung weg → Dashboard zeigt Tag 3 wieder rot als Anomalie, KPI wieder höher.

- [ ] **Step 7: Aufräumen**

```bash
kill $API_PID $UI_PID 2>/dev/null
rm -f CoffeeApi/coffee.db
```

Falls irgendein Schritt schiefgeht, notiere die genaue Stelle (Browser DevTools Network-Tab hilft) und diagnostiziere — nicht committen bevor alles klappt.

---

## Task 19: Docs + PROJECT_STATE

**Files:**
- Modify: `README.md`
- Modify: `PROJECT_STATE.md`

- [ ] **Step 1: README — neue Dashboard-Feature + Endpoint**

In der Tabelle `## API Endpoints` (README.md:102-112) drei Zeilen hinzufügen:

```markdown
| GET | `/api/stats/excluded-days` | Tage, die als Massenimport markiert sind | - |
| POST | `/api/stats/excluded-days` | Tag als Massenimport markieren | - |
| DELETE | `/api/stats/excluded-days/{date}` | Markierung aufheben | - |
```

In der Tabelle `## Dashboard Features` (README.md:129-143) eine Zeile anfügen:

```markdown
| Massenimport-Markierung | Tage ueber Log-Tab als Backfill markieren, grau mit Label im Chart, aus Statistik ausgeschlossen |
```

- [ ] **Step 2: PROJECT_STATE.md — Aenderungshistorie**

Ergänze:

```markdown
| 2026-04-18 | Massenimport-Flag: Tage per Log-Tab markierbar, aus Stats/Anomalie ausgeblendet |
```

- [ ] **Step 3: Commit**

```bash
git add README.md PROJECT_STATE.md
git commit -m "docs: document mass-import flag feature and endpoints"
```

---

## Task 20: Merge + Deploy

- [ ] **Step 1: MR `feat/mass-import-flag` → `main` öffnen**

Pipeline aus Plan 1 läuft, API+Dashboard-Images werden mit neuer Migration gebaut.

- [ ] **Step 2: Merge**

- [ ] **Step 3: Deploy**

Im Portainer: Stack Update → neue Images ziehen → Container neu starten. Der API-Container läuft `Migrate()` und legt beim ersten Start die `ExcludedDays`-Tabelle auf der Prod-DB an (die vorher via Plan 2 Task 7 baselined wurde — Migration `AddExcludedDays` ist die zweite in History).

- [ ] **Step 4: Smoke-Test auf Prod**

1. Öffne Dashboard.
2. Gehe zu Log.
3. Suche den historischen 30-Kaffee-Tag (Feb 2026, BSH-Ausfall).
4. Klicke "markieren" → Reason: "BSH API Ausfall, Accumulierte Zähler erst nach 1 Woche erhalten".
5. Zurück zum Dashboard → Tag ist grau, Anomalie weg, Wochen-KPI realistischer.

Erledigt.

---

## Self-Review

- ✅ **Spec coverage:**
  - Backend-Table + Migration: Tasks 1-3 ✔
  - CRUD-Endpoints (GET/POST/DELETE): Tasks 5-7 ✔
  - LogPage-Action mit Reason-Modal: Tasks 12-13 ✔
  - DailyBarChart grau + Label: Task 14 ✔
  - Aus Tag/Woche/Monat-Aggregation raus: Task 16 ✔
  - Aus Anomaly-Detection raus: Task 15 ✔
  - Gesamt-Counter bleibt (User hat das explizit so gewünscht, weil "Bezüge sind gültig"): Task 16 `isAll`-Branch unverändert ✔
  - Dokumentation: Task 19 ✔

- ✅ **Placeholder scan:** keine TBDs, alle Tests vollständig, alle Commands lauffähig.

- ✅ **Type consistency:**
  - `excludedSet: Set<string>` — Tasks 14, 15, 16, 17 alle konsistent
  - `buildExcludedSet(excluded.data)` aus `excludedDayUtils.ts` — Tasks 13, 17 konsistent
  - `useAddExcludedDay`, `useRemoveExcludedDay`, `useExcludedDays` — in Task 10 definiert, in Tasks 12, 13 genutzt — gleiche Namen
  - Backend-Properties: `Date` (DateOnly) in C#, `date` (string) im DTO/TS — beide konsistent genutzt; `yyyy-MM-dd` ist die Transport-Form.
  - Route-Pfad `/api/stats/excluded-days` — Controller (Task 5) + Client (Task 9) identisch.
