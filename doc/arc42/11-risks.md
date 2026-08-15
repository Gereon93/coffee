# 11. Risks and Technical Debt

State verified against the code and a full test run: 87 xUnit tests
(`CoffeeTest/`) and 100 Vitest tests (`coffee-dashboard/`), all passing. Items
already fixed have been removed from this list rather than left as noise.

Severity is about impact on this system in its intended deployment, not a
generic CVSS-style score.

## 11.1 Technical Debt

### Security

| ID | Item | Severity | Detail |
|----|------|----------|--------|
| TD-01 | **Write endpoints unauthenticated** | Medium | `ApiKeyMiddleware.ProtectedPaths` still covers only `/api/ingest`; `POST /coffee/power` and `POST` / `DELETE /api/stats/marked-days` remain open to anyone on the LAN. The cross-origin half is closed: CORS now allows only the origins in `Cors:AllowedOrigins` (default: the dev and dashboard origins), so a hostile page can no longer drive these endpoints from a LAN browser. The 07:00–18:00 lock in `coffeeTimeLock.ts` is client-side and protects nothing. |
| TD-02 | **Missing `ApiKey` silently disables ingest authentication** | Medium | `ApiKeyMiddleware` logs a warning and forwards the request when no key is configured. A configuration mistake in production removes authentication instead of failing loudly. Failing closed in `Production` and open only in `Development` would match the intent. |
| TD-03 | **Known-vulnerable transitive dependency** | Medium | `dotnet restore` emits `NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.10 has a known high severity vulnerability` (GHSA-2m69-gcr7-jv3q), pulled in by `Microsoft.EntityFrameworkCore.Sqlite` 9.0.0. It is a warning; nothing fails. `<TreatWarningsAsErrors>` for `NU1903`, or `NuGetAudit` set to error, would gate it. |
| TD-04 | **No forwarded-headers handling** | Low | The API sits behind nginx but does not use `UseForwardedHeaders`. The remote address logged on a failed API-key attempt is the proxy's, making that log line useless for its purpose. |
| TD-05 | **API documentation served unauthenticated in production** | Low | Scalar (`/scalar/v1`) and `/openapi/v1.json` are proxied by nginx and reachable by anyone on the LAN. Acceptable under TC-3; becomes a finding the moment the deployment model changes. |
| TD-06 | **No rate limiting** | Low | No throttling anywhere. `/coffee/power` relays straight to n8n and therefore to BSH. |

### Correctness

| ID | Item | Severity | Detail |
|----|------|----------|--------|
| TD-07 | **Counter reset is not handled** | Medium | After a reset all counters are lower, never higher, so `HasCounterIncreased` returns false and nothing is stored. The baseline stays at the pre-reset maximum and every delta clamps to 0 until the counters climb past it. Detecting a decrease and starting a new epoch is the fix. See [ADR-005](09-design.md#adr-005-counter-based-idempotency). |
| TD-08 | **`MachineId` filtering is inconsistent** | Medium (latent) | `GetLatestAsync` filters by `MachineId`; `GetAllAsync`, `GetByDateAsync`, `GetByDateRangeAsync`, `GetLastSnapshotBeforeAsync`, and `GetHeatmapDataAsync` do not. Harmless while exactly one machine exists — and silently wrong the first time a second one ingests. Either filter consistently or drop the field and the seam it implies. |
| TD-09 | **Frontend write calls bypass the API client** | Medium | `addMarkedDay`, `removeMarkedDay` (`src/api/stats.ts`) and `setCoffeePower` (`src/api/coffee.ts`) call `fetch` directly with hardcoded relative paths, ignoring `fetchJson` and `BASE_URL`. Reads honour `VITE_API_BASE_URL`, writes do not — so in any split-origin setup reads work and writes 404. They also duplicate error handling and throw a bare `Error` instead of `ApiError`. |
| TD-10 | **Vite dev proxy does not cover `/coffee`** | Low | `vite.config.ts` proxies only `/api`. Under `npm run dev` the power button and the live status widget hit the dev server and fail. Local development of those features needs undocumented extra configuration. |
| TD-11 | **Fixed UTC offset is not DST-aware** | Low | `tz` is a single offset applied to every date in a request. Ranges spanning a CET/CEST transition shift by one hour on the far side. Accepted trade-off, see [ADR-004](09-design.md#adr-004-client-driven-timezone-offset). |
| TD-27 | **`/api/health` cannot report a broken database** | Medium | `Health()` awaits `_snapshotService.GetLatestAsync()` *before* evaluating `CanConnectAsync()`, without a `try`/`catch`. If SQLite is unavailable the query throws and the endpoint answers 5xx — the `database: "disconnected"` branch is unreachable for precisely the failure it exists for. `ApiIntegrationTests.Health_ReturnsOk` only exercises a reachable database, so nothing catches this. Either wrap the probe or drop the field. |

### Architecture and code quality

| ID | Item | Severity | Detail |
|----|------|----------|--------|
| TD-12 | **`StatsController` violates the documented layering** | Medium | It injects `AppDbContext` directly and performs the entire range aggregation — grouping by local date, rolling the baseline, computing deltas — inside the action. `CLAUDE.md` mandates Controller → Service → EF Core, and the delta arithmetic already exists in `SnapshotService.GetDailySummaryAsync`. Two copies of the same rule in two layers is how they drift apart. |
| TD-13 | **`SnapshotService` carries five responsibilities** | Medium | 307 LOC and an 8-member interface spanning ingest and idempotency, Home Connect payload parsing, plain queries, statistical aggregation, and timezone arithmetic. It is not a god class by size, but it is one by cohesion: nothing about parsing `JsonElement` values belongs in the same type as heatmap bucketing. A natural split is a payload mapper, an ingest service, a query service, and a statistics service. |
| TD-14 | **`GetLocalDayBoundsUtc` is duplicated verbatim** | Low | The same private method exists in both `SnapshotService` and `StatsController`. The day-boundary rule is core domain logic and must have exactly one definition. |
| TD-16 | **Unused composite index** | Low | `IX_MachineSnapshots_Idempotency` over `(MachineId, Coffee, CoffeeAndMilk, Milk)` is declared for an idempotency strategy the code does not use — the check fetches the latest row by timestamp. It costs write throughput and buys nothing. |
| TD-17 | **Magic numbers without names** | Low | Anomaly threshold `1.5` (`anomalyUtils.ts`, `useAnomalyDetection.ts`), the `'2020-01-01'` epoch for the `all` period (`dateUtils.ts`), the `3000` ms post-power settle delay (`useCoffeeStatus.ts`), the page-size cap `100`, and the heatmap week cap `52`. `AGENTS.md` requires these to be named constants. |
| TD-18 | **Hardcoded `ProductVersion` in the baseliner** | Low | `MigrationBaseliner` writes `'9.0.0'` into `__EFMigrationsHistory`. Cosmetic — EF Core does not read it back — but it will be wrong and confusing after an EF upgrade. |
| TD-19 | **`UseHttpsRedirection` with no HTTPS port** | Low | The container listens on HTTP only (`ASPNETCORE_URLS=http://+:8080`), so the middleware logs a "failed to determine the https port" warning on startup and does nothing. Dead configuration. |

### Quality gates and tooling

| ID | Item | Severity | Detail |
|----|------|----------|--------|
| TD-22 | **No React error boundary** | Medium | Sentry is initialised but no boundary is mounted. A render-time exception in any chart takes the whole page to a blank screen instead of a contained error state — despite `ErrorMessage` already existing for the data-fetch case. |
| TD-23 | **Package versions trail the target framework** | Low | The projects target `net10.0` while `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, and `Microsoft.AspNetCore.OpenApi` are pinned to `9.0.0`. It works by roll-forward, and it is also the reason for TD-03. |
| TD-24 | **Divergent image tagging** | Low | CI tags `:latest` and `:sha-<short>`; `build.sh` tags `:latest` and `:YYYYmmdd-HHMMSS`. A locally built image cannot be traced back to a commit. |

## 11.2 Risk Register

| Risk | Probability | Impact | Exposure | Mitigation / status |
|------|-------------|--------|----------|---------------------|
| **n8n stops and nobody notices** | Medium | High | The API looks perfectly healthy; the dashboard just shows a flat line that could be real. | `/api/health` exposes `lastSnapshot`, but nothing alerts on it. An alert on snapshot staleness is the highest-value missing safeguard. |
| **Drive-by actuation of the machine** | Very low | Medium | CORS is restricted to configured origins, so a hostile page can no longer drive the write endpoints; a direct LAN client still can. | Put the write endpoints behind the API key (TD-01). |
| **Counter reset corrupts statistics** | Low | Medium | Silent: numbers stay plausible while being wrong. | Unhandled (TD-07). |
| **Frontend regression reaches production** | Low | Medium | A dependency bump now passes lint, type-check, unit tests and a production build before it can reach `main`. | Covered by the `dashboard` job in `ci.yml`. |
| **SQLite file corruption or NAS failure** | Low | High | Total data loss; the history cannot be reconstructed, since the machine only knows lifetime totals. | Backup is a file copy — but there is no scheduled backup documented or automated. |
| **BSH changes the Home Connect API** | Low | High | Ingest stops or delivers unmapped keys. | The n8n layer absorbs shape changes; unknown keys are silently ignored by the `switch` in `MapToEntity` — which also means a *renamed* counter key degrades quietly rather than failing. |
| **Known vulnerability in the SQLite native library** | Low | Medium | Reachable only via crafted database input; the file is not attacker-controlled. | Upgrade the EF Core packages (TD-03, TD-23). |
| **SQLite single-writer contention** | Very low | Low | One writer by construction. | None needed. |
| **.NET 10 end of life** | Low | Medium | Supported into 2028. | Routine upgrade planning. |

## 11.3 Suggested Order of Work

Ranked by risk removed per unit of effort, not by severity alone.

1. **TD-01** — API key on the write endpoints. Small diff, and it closes the remaining half of the only risk in this list with a physical effect.
2. **TD-03 / TD-23** — bump EF Core and OpenAPI packages to the .NET 10 line; clears the vulnerability warning as a side effect.
3. **TD-09 / TD-10 / TD-27** — route frontend writes through `fetchJson`, extend the dev proxy to `/coffee`, and make the health probe survive a broken database. All three are small and each removes a case where the system misreports its own state.
4. **TD-12 / TD-14** — move the range aggregation into `SnapshotService` and delete the duplicated day-bounds method. One behaviour, one home.
5. **TD-22** — an error boundary around the routes, so a render-time exception in a chart cannot blank the page.
6. **TD-02, TD-07, TD-13** — deeper changes with real design questions attached; worth their own discussion rather than a drive-by fix.
