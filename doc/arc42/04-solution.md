# 4. Solution Strategy

This section gives the short version of *why the system looks the way it does*.
Individual decisions with their alternatives and consequences are recorded as
ADRs in [09 — Design Decisions](09-design.md).

## 4.1 Technology Decisions

| Area | Choice | Driving quality goal |
|------|--------|---------------------|
| Backend | ASP.NET Core (.NET 10), controller-based | Correctness (static typing, analyzers), operational simplicity (single self-contained container) |
| Persistence | SQLite via EF Core 9 | Operational simplicity — one file, no server, backup by copy |
| Cloud access | Delegated entirely to n8n | Security (no inbound ports, no OAuth2 secrets in the API), simplicity |
| API docs | Scalar over Swashbuckle | Developer experience; native OpenAPI via `Microsoft.AspNetCore.OpenApi` |
| Frontend | React 19 + Vite + TypeScript | Charting ecosystem (Recharts), fast iteration |
| Server state | TanStack Query | Caching, retry, and invalidation without hand-written state machines |
| Styling | Tailwind CSS v4 | Utility-first, dark mode via system preference, no separate CSS build step |
| Errors | Sentry SDK → self-hosted GlitchTip | Observability without SaaS (OC-2) |
| Delivery | Docker images → GHCR → Portainer | Reproducible deployment on a NAS without an orchestrator |

## 4.2 Decomposition Strategy

The system splits along the **deployment boundary**, not along features: one
API container, one static-frontend container, one external automation platform.

Inside the API, the layering is `Controller → Service → EF Core`:

- **Controller** — HTTP concerns only: binding, validation of the request
  shape, status-code selection, DTO mapping.
- **Service** — all business rules: idempotency, day boundaries, delta and
  peak computation, aggregation, validation of domain rules.
- **EF Core / `AppDbContext`** — persistence and model configuration.

Entities never cross the HTTP boundary; DTOs are mapped in the controller.

> The layering is a convention, not a compiler-enforced rule.
> `StatsController` currently injects `AppDbContext` directly and performs
> range aggregation itself — a known deviation, tracked in
> [11 — Risks and Technical Debt](11-risks.md).

The frontend mirrors the same idea: `api/` (transport) → `hooks/` (server
state) → `components/` + `pages/` (rendering), with pure logic isolated in
`lib/` so it can be reasoned about without React.

## 4.3 Handling the Core Domain Problem: Counters, not Events

The machine reports lifetime totals. Three rules turn that into usable data.

### Rule 1 — Consumption is a delta

`consumption(period) = counter(last sample in period) − counter(baseline)`.

### Rule 2 — The baseline comes from *before* the period

If the first sample of the day is at 07:15 and three coffees were brewed at
06:40, using the day's own first sample as the baseline loses them. The
baseline is therefore the **last snapshot strictly before the local start of
the period**. Only when no such snapshot exists (the very first day of the
dataset) does the period's own first sample serve as the baseline.

Both `SnapshotService.GetDailySummaryAsync` and `StatsController.GetRange`
apply this. In the range case the baseline rolls forward: each day's baseline
is the previous day's last snapshot.

### Rule 3 — Store a sample only when it carries information

n8n delivers a payload every 15 minutes whether or not anyone drank coffee.
Persisting all of them would mean ~76 rows/day of which most are identical.
A row is written only if at least one beverage counter increased.

The trade-off: status-only changes (`OperationState`, illumination) are never
recorded after the first snapshot, and a **counter reset is invisible** —
after a reset all counters are lower, never higher, so nothing is written and
the stored baseline stays at the old maximum. See
[ADR-005](09-design.md#adr-005-counter-based-idempotency).

## 4.4 Time Strategy

- Everything is stored in UTC. `DateTime.UtcNow` is the only clock source.
- SQLite does not preserve `DateTimeKind`, so `AppDbContext.OnModelCreating`
  installs a global `ValueConverter` that re-stamps every `DateTime` property
  as `DateTimeKind.Utc` on read. Without it, JSON serialisation would emit
  timestamps without the `Z` suffix and the browser would read them as local.
- The **client** supplies the timezone as a fixed offset in minutes (`tz`),
  taken from `-new Date().getTimezoneOffset()`.
- The backend derives a half-open UTC interval:
  `[localMidnight − tz, localMidnight − tz + 24 h)`.
- Grouping keys (peak hour, heatmap cell) are computed as
  `timestamp.AddMinutes(tz)`.

**Known limitation:** a fixed offset is not a timezone. A 52-week heatmap
requested in July applies the CEST offset to January samples, shifting their
local midnight by one hour. Accepted for a single-timezone household; a
zone-aware fix would pass `Europe/Berlin` and use `TimeZoneInfo`. See
[ADR-004](09-design.md#adr-004-client-driven-timezone-offset).

## 4.5 Safety and Degradation Strategy

| Failure | Behaviour |
|---------|-----------|
| n8n unreachable for status | `GET /coffee/status` returns 200 with `reachable: false`, `label: "Offline"` — the dashboard degrades, it does not error |
| n8n unreachable for power | `POST /coffee/power` returns 500 with a generic message; the exception is logged and sent to GlitchTip |
| Ingest fails unexpectedly | 500 with a generic body; no internal detail is echoed to the caller |
| Database unreachable | `GET /api/health` reports `database: "disconnected"` |
| Sentry DSN not configured | Error tracking silently disables — local development needs no network |

## 4.6 Migration Strategy

EF Core migrations are applied automatically at container startup
(`db.Database.Migrate()` in `Program.cs`).

The production database predates migrations — it was created with
`EnsureCreated()` and therefore has no `__EFMigrationsHistory` table.
`MigrationBaseliner.EnsureBaselined` detects exactly this situation (schema
tables present, history table absent) and seeds the history with the initial
migration ID so `Migrate()` applies only what is genuinely pending instead of
trying to recreate existing tables. The routine is idempotent and a no-op on
fresh databases. See [ADR-007](09-design.md#adr-007-migration-baseliner).

## 4.7 Quality Assurance Strategy

| Level | Mechanism |
|-------|-----------|
| Compile time | Nullable reference types, TypeScript strict mode, ESLint |
| Unit | xUnit over `Services/` and domain logic, EF Core InMemory provider |
| Controller | Every branch of every controller action, including error paths |
| Integration | `WebApplicationFactory` against a real temporary SQLite file — exercises the full HTTP pipeline, middleware, and migrations |
| CI | `.github/workflows/ci.yml` — restore, build, test on every push and PR |
| Static analysis | SonarQube scan on `main` (`sonar.yml`). Automated LLM review on PRs is currently disabled |

Current state: **81 tests, all passing.** The frontend has no automated tests —
a gap recorded in [11](11-risks.md).
