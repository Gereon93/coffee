# 11. Risks and Technical Debt

State verified against the code and a full test run: 153 xUnit tests
(`CoffeeTest/`) and 102 Vitest tests (`coffee-dashboard/`), all passing. Items
already fixed have been removed from this list rather than left as noise.

Severity is about impact on this system in its intended deployment, not a
generic CVSS-style score.

## 11.1 Technical Debt

### Security

| ID | Item | Severity | Detail |
|----|------|----------|--------|
| TD-01 | **Write endpoints unauthenticated** | ~~Medium~~ **Resolved** | `ApiKeyMiddleware` now protects `POST /coffee/power` and `POST` / `DELETE /api/stats/marked-days` alongside `/api/ingest`; the route table is method-aware, so the reads on those paths stay open. The dashboard's nginx injects the key from the `API_KEY` container variable, keeping it out of the browser bundle. Residual: anything that reaches the dashboard port is served the key by that proxy — the same reach as pressing the button in the UI. Closing that needs user authentication, not a shared secret. See [8.4](08-concepts.md#84-security). |
| TD-02 | **Missing `ApiKey` silently disables ingest authentication** | ~~Medium~~ **Resolved** | `ApiKeyMiddleware` now rejects every protected request with `503 Service Unavailable` in `Production` when `ApiKey` is missing or whitespace-only; in `Development` it still allows the request. Design choice: per-request fail-closed rather than aborting startup, so health and reads stay up. In the documented Portainer deployment the key is an environment variable snapshotted at process start — supplying it later still requires recreating the container before protected writes succeed. |
| TD-03 | **Known-vulnerable transitive dependency** | Medium | `dotnet restore` emits `NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.10 has a known high severity vulnerability` (GHSA-2m69-gcr7-jv3q), pulled in by `Microsoft.EntityFrameworkCore.Sqlite` 9.0.0. It is a warning; nothing fails. `<TreatWarningsAsErrors>` for `NU1903`, or `NuGetAudit` set to error, would gate it. |
| TD-04 | **No forwarded-headers handling** | ~~Low~~ **Resolved** | The API honours `X-Forwarded-For` when `ForwardedHeaders:KnownNetworks` names the proxy network, and the dashboard's nginx now sends the header. Unconfigured it stays off on purpose: the API port is reachable on the LAN, and an unrestricted forwarder would let a direct caller write any address it likes into the log. |
| TD-05 | **API documentation served unauthenticated in production** | ~~Low~~ **Resolved** | `MapOpenApi` and `MapScalarApiReference` only run in `Development`, and nginx no longer proxies `/scalar/` or `/openapi/`. In production both answer `404`. |
| TD-06 | **No rate limiting** | ~~Low~~ **Resolved** | `POST /coffee/power` runs under a fixed-window limiter (10 requests per minute, `429` beyond it) that sits in front of the API-key check, so unauthenticated attempts consume permits too. The remaining endpoints are reads against local SQLite and are not throttled. |

### Correctness

| ID | Item | Severity | Detail |
|----|------|----------|--------|
| ~~TD-07~~ | **~~Counter reset is not handled~~** | ~~Medium~~ **Resolved** | A decrease in any cup counter is persisted and treated as a new epoch. Daily and range totals are computed by summing only positive per-pair increments, so the first snapshot after a reset becomes the new baseline. Residual: without a trusted upstream ordering token ([ADR-009](09-design.md#adr-009-server-side-ingest-timestamp)), a delayed poll can still look like a reset — accepted, see [ADR-015](09-design.md#adr-015-counter-reset-detection). |
| TD-08 | **`MachineId` filtering is inconsistent** | Medium (latent) | `GetLatestAsync` filters by `MachineId`; `GetAllAsync`, `GetByDateAsync`, `GetByDateRangeAsync`, `GetLastSnapshotBeforeAsync`, and `GetHeatmapDataAsync` do not. Harmless while exactly one machine exists — and silently wrong the first time a second one ingests. Either filter consistently or drop the field and the seam it implies. |
| TD-09 | **Frontend write calls bypass the API client** | ~~Medium~~ **Resolved** | All writes now go through `fetchJson`/`BASE_URL` and throw `ApiError`; the duplicated error handling is gone. `fetchJson` surfaces the server-provided `error`/`message` and tolerates bodyless responses such as `204` on `DELETE`. |
| TD-10 | **Vite dev proxy does not cover `/coffee`** | ~~Low~~ **Resolved** | `vite.config.ts` proxies `/coffee` to the same target as `/api`, so the power button and the live status widget work under `npm run dev`. |
| TD-11 | **Fixed UTC offset is not DST-aware** | Low | `tz` is a single offset applied to every date in a request. Ranges spanning a CET/CEST transition shift by one hour on the far side. Accepted trade-off, see [ADR-004](09-design.md#adr-004-client-driven-timezone-offset). |
| TD-27 | **`/api/health` cannot report a broken database** | Medium | `Health()` awaits `_snapshotService.GetLatestAsync()` *before* evaluating `CanConnectAsync()`, without a `try`/`catch`. If SQLite is unavailable the query throws and the endpoint answers 5xx — the `database: "disconnected"` branch is unreachable for precisely the failure it exists for. `ApiIntegrationTests.Health_ReturnsOk` only exercises a reachable database, so nothing catches this. Either wrap the probe or drop the field. |

### Architecture and code quality

| ID | Item | Severity | Detail |
|----|------|----------|--------|
| TD-12 | **`StatsController` violates the documented layering** | ~~Medium~~ **Resolved** | The range aggregation moved into `SnapshotStatisticsService.GetRangeAggregateAsync`; the controller validates the two dates and maps the result. No controller injects `AppDbContext` any more. See [ADR-012](09-design.md#adr-012-snapshot-services-split-by-responsibility). |
| TD-13 | **`SnapshotService` carries five responsibilities** | ~~Medium~~ **Resolved** | Split into `LocalDay`, `SnapshotPayloadMapper`, `SnapshotQueryService`, `SnapshotIngestService`, and `SnapshotStatisticsService`; `ISnapshotService` is gone and every caller depends only on what it uses. See [ADR-012](09-design.md#adr-012-snapshot-services-split-by-responsibility). |
| TD-14 | **`GetLocalDayBoundsUtc` is duplicated verbatim** | ~~Low~~ **Resolved** | The rule now has exactly one definition: `LocalDay.BoundsUtc` in `Domain/`, covered by `LocalDayTests`. |
| TD-16 | **Unused composite index** | ~~Low~~ **Resolved** | `IX_MachineSnapshots_Idempotency` is gone from the model and dropped by migration `DropUnusedIdempotencyIndex`. The idempotency check still reads the latest row by timestamp, which `IX_MachineSnapshots_Timestamp` serves. |
| TD-17 | **Magic numbers without names** | ~~Low~~ **Resolved** | Now `ANOMALY_Z_SCORE_THRESHOLD`, `ALL_TIME_START_DATE`, `POWER_SETTLE_DELAY_MS`, `SnapshotQueryService.MaxPageSize` (which `StatsController` reuses instead of repeating the literal) and `StatsController.MaxHeatmapWeeks`. |
| TD-18 | **Hardcoded `ProductVersion` in the baseliner** | ~~Low~~ **Resolved** | `MigrationBaseliner` writes `ProductInfo.GetVersion()`, so the row records the EF Core version that actually ran. |
| TD-19 | **`UseHttpsRedirection` with no HTTPS port** | ~~Low~~ **Resolved** | The call is gone; a comment in `Program.cs` records that the container serves plain HTTP and TLS terminates upstream. |

### Quality gates and tooling

| ID | Item | Severity | Detail |
|----|------|----------|--------|
| TD-22 | **No React error boundary** | Medium | Sentry is initialised but no boundary is mounted. A render-time exception in any chart takes the whole page to a blank screen instead of a contained error state — despite `ErrorMessage` already existing for the data-fetch case. |
| TD-23 | **Package versions trail the target framework** | Low | The projects target `net10.0` while `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, and `Microsoft.AspNetCore.OpenApi` are pinned to `9.0.0`. It works by roll-forward, and it is also the reason for TD-03. |
| TD-24 | **Divergent image tagging** | ~~Low~~ **Resolved** | `build.sh` tags `:latest` and `:sha-<short>` like CI does, and appends `-dirty` when the working tree has uncommitted changes rather than claiming a commit it does not match. |

## 11.2 Risk Register

| Risk | Probability | Impact | Exposure | Mitigation / status |
|------|-------------|--------|----------|---------------------|
| **n8n stops and nobody notices** | Medium | High | The flat line in the dashboard is no longer the only signal. | Covered by `IngestWatchdog`: an `Error` log — and therefore a GlitchTip event — once the newest snapshot is older than `Watchdog:StaleAfterMinutes` (60 by default), suppressed during the nightly window without a scheduled ingest. |
| **The API or the NAS dies and nobody notices** | Low | High | No alarm at all: the watchdog runs *inside* the API, so it is silent exactly when the process is not running. | Uncovered by design — closing it needs a probe outside the API (a heartbeat the ingest pings, or a monitor inside the LAN polling `/api/health`). |
| **Drive-by actuation of the machine** | Very low | Medium | CORS is restricted to configured origins and the write endpoints require `X-API-Key`, so neither a hostile page nor a direct LAN client can actuate the machine. | Closed by TD-01. A caller that reaches the dashboard proxy is still served the key — that residual needs user authentication. |
| **Counter reset corrupts statistics** | Low | Medium | Silent: numbers stay plausible while being wrong. | Handled by ADR-015: resets are persisted and statistics re-anchor at the first post-reset snapshot. |
| **Frontend regression reaches production** | Low | Medium | A dependency bump now passes lint, type-check, unit tests and a production build before it can reach `main`. | Covered by the `dashboard` job in `ci.yml`. |
| **SQLite file corruption or NAS failure** | Low | High | Total data loss; the history cannot be reconstructed, since the machine only knows lifetime totals. | Backup is a file copy — but there is no scheduled backup documented or automated. |
| **BSH changes the Home Connect API** | Low | High | Ingest stops or delivers unmapped keys. | The n8n layer absorbs shape changes; unknown keys are silently ignored by the `switch` in `MapToEntity` — which also means a *renamed* counter key degrades quietly rather than failing. |
| **Known vulnerability in the SQLite native library** | Low | Medium | Reachable only via crafted database input; the file is not attacker-controlled. | Upgrade the EF Core packages (TD-03, TD-23). |
| **SQLite single-writer contention** | Very low | Low | One writer by construction. | None needed. |
| **.NET 10 end of life** | Low | Medium | Supported into 2028. | Routine upgrade planning. |

## 11.3 Suggested Order of Work

Ranked by risk removed per unit of effort, not by severity alone.

1. **TD-03 / TD-23** — bump EF Core and OpenAPI packages to the .NET 10 line; clears the vulnerability warning as a side effect.
2. **TD-27** — make the health probe survive a broken database. Small fix that removes a case where the system misreports its own state.
3. **TD-22** — an error boundary around the routes, so a render-time exception in a chart cannot blank the page.
4. **TD-08** — filter `MachineId` consistently across statistics queries (latent until a second machine appears).
