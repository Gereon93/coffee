# 9. Design Decisions

Architecture decision records, newest last. Each records the situation, the
choice, the alternatives that were rejected, and what the project has to live
with as a result.

---

## ADR-001: SQLite over MongoDB

**Status:** accepted

**Context.** The predecessor system (built around a Nivona machine) used
MongoDB. After the pivot to the EQ900 the storage layer was reconsidered from
scratch. The workload is roughly 76 writes per day, single writer, and the
data is flat and relational.

**Decision.** SQLite via EF Core.

**Alternatives rejected.**
- *MongoDB* — a server process, a container, and a backup strategy for data
  that has no document shape.
- *PostgreSQL* — the same operational weight for a dataset measured in
  megabytes.
- *Flat files / JSON* — no query capability, no schema evolution path.

**Consequences.** Backup is `cp coffee.db`. No server, no port, no
credentials. In exchange: one writer at a time, no horizontal scaling, and
aggregation happens in application memory rather than in the database. All
acceptable at this scale.

---

## ADR-002: n8n as Cloud Gateway

**Status:** accepted

**Context.** Home Connect requires OAuth2 with refresh-token rotation, is
rate-limited, and needs a scheduler. An n8n instance already existed in the
LAN with internet access and credential storage.

**Decision.** All communication with BSH goes through n8n. The API never
speaks to the cloud; it only exchanges LAN HTTP with n8n.

**Alternatives rejected.**
- *OAuth2 client in the API* — token storage, refresh handling, retry and
  backoff logic, plus outbound internet from the NAS.
- *Home Connect event stream (SSE)* — a long-lived connection to maintain and
  reconnect; polling is sufficient at 15-minute resolution.

**Consequences.** The API holds no cloud credentials and needs no inbound
internet exposure. n8n becomes a hard dependency and a single point of
failure, and debugging spans two systems. Crucially, the API cannot
distinguish "no coffee was made" from "n8n is down" — which is why
`/api/health` exposes `lastSnapshot`.

---

## ADR-003: Scalar over Swagger/Swashbuckle

**Status:** accepted

**Context.** An interactive API surface is wanted for development and for
debugging the n8n integration.

**Decision.** `Scalar.AspNetCore` on top of `Microsoft.AspNetCore.OpenApi`,
mapped in `Development` only — in production nothing consumes it and it would
hand the whole contract to anyone reaching the port.

**Alternatives rejected.** Swashbuckle — heavier, and .NET's built-in OpenAPI
document generation already covers document generation.

**Consequences.** Modern UI, smaller ecosystem. The docs are served
unauthenticated in production; see [08.4](08-concepts.md#84-security).

---

## ADR-004: Client-Driven Timezone Offset

**Status:** accepted, with a known limitation

**Context.** Timestamps are stored in UTC, but "how much coffee today" is a
question about a *local* calendar day. Something has to decide where local
midnight falls.

**Decision.** The client sends its current UTC offset in minutes as a `tz`
query parameter. The backend uses it to derive day boundaries and grouping
keys.

**Alternatives rejected.**
- *Hardcode `Europe/Berlin` on the server* — simple and correct for this
  household, but bakes a deployment assumption into the domain logic.
- *Send an IANA zone id and use `TimeZoneInfo`* — fully correct across DST
  boundaries, at the cost of zone-database handling and per-date conversion.
  This is the natural upgrade path if the limitation below ever bites.

**Consequences.** Every time-aware endpoint carries a `tz` parameter, injected
transparently by `src/api/stats.ts`. Because a single fixed offset is applied
to all dates in a request, ranges spanning a DST change shift by one hour for
the dates on the other side of the transition.

---

## ADR-005: Counter-Based Idempotency

**Status:** accepted, with a known limitation

**Context.** n8n delivers a payload every 15 minutes regardless of activity,
retries on failure, and can replay. Storing every payload would produce ~76
rows/day, most of them identical.

**Decision.** Persist a snapshot only when at least one beverage counter has
strictly increased.

**Alternatives rejected.**
- *Idempotency key from n8n* — pushes state into the workflow and does not
  reduce storage.
- *Store everything and deduplicate on read* — more rows, and every read query
  gets more complex.
- *Unique constraint on the counter tuple* — would reject legitimate later
  snapshots after a counter reset.

**Consequences.**
- Retries are inherently safe; the response returns the stored snapshot's id.
- Status-only changes are never recorded after the first snapshot.
- Hot water drawn without a cup count change writes nothing.
- **A counter reset is invisible.** After a reset all counters are lower, so
  nothing is written and the baseline stays at the old maximum; deltas clamp
  to 0 until the counters climb past it. Handling this would mean detecting a
  decrease and treating it as a new epoch — currently unimplemented, tracked
  in [11](11-risks.md).

---

## ADR-006: React over Blazor

**Status:** accepted

**Context.** The predecessor frontend was Blazor (`CoffeeWeb`). The dashboard
is chart-heavy and iterated on frequently.

**Decision.** React 19 + Vite + TypeScript, with Recharts and TanStack Query.

**Alternatives rejected.** Blazor — one runtime instead of two, but a weaker
charting ecosystem and a slower edit-refresh loop.

**Consequences.** Two toolchains (.NET and Node) to keep current, and two
dependency ecosystems for Dependabot. In exchange, the chart layer is
straightforward and the dev loop is fast.

---

## ADR-007: Migration Baseliner

**Status:** accepted

**Context.** The production database was created with `EnsureCreated()` before
migrations were introduced. It has the right schema but no
`__EFMigrationsHistory` table, so `Database.Migrate()` would try to create
tables that already exist and fail.

**Decision.** `MigrationBaseliner.EnsureBaselined` runs before `Migrate()`. It
detects the exact signature of a legacy database — schema tables present,
history table absent — and seeds the history with the first migration id
inside a transaction.

**Alternatives rejected.**
- *Manual one-off SQL on the NAS* — an undocumented step that would have to be
  repeated on every restore from an old backup.
- *Drop and recreate* — data loss.

**Consequences.** Fresh databases skip the baseliner; already-baselined ones
skip it too; the routine is idempotent and safe on every startup. The seeded
`ProductVersion` string is hardcoded and is cosmetic only.

---

## ADR-008: MarkedDay Dual-Kind Design

**Status:** accepted

**Context.** Two distinct needs appeared: exclude backfilled days from
statistics, and explain days that look anomalous for a known reason.

**Decision.** One `MarkedDay` entity keyed by date, with a `Kind`
discriminator (`mass-import` | `event`) that selects both the validation rules
and the statistical semantics.

**Alternatives rejected.**
- *Two tables* — near-identical structure, duplicated CRUD, and a new question
  ("can a day be in both?") that this design answers by construction.
- *A boolean `IsExcluded` flag* — no room for the event taxonomy.

**Consequences.** A date can carry exactly one annotation. Adding a third kind
means extending a string enum and its validation rather than adding a table.
The frontend derives its lookup maps from a single fetch (`markedDayUtils.ts`).

---

## ADR-009: Server-Side Ingest Timestamp

**Status:** accepted

**Context.** The Home Connect payload contains its own timestamps.
`SnapshotPayloadMapper` ignores them; `SnapshotIngestService` stamps
`Timestamp = DateTime.UtcNow`.

**Decision.** The API's receive time is authoritative for a snapshot.

**Rationale.** The payload timestamp describes when *n8n* read the machine,
which is itself only accurate to the polling interval; it arrives in an
unvalidated format from an upstream that could change it. A server-side clock
is monotonic within the process, always present, and always in a known
timezone. Given a 15-minute sampling grid, the difference between "n8n read
it" and "the API stored it" is noise.

**Consequences.** Snapshot times reflect ingest, not brewing. Hourly
attribution is only ever accurate to the polling interval — a coffee brewed at
07:59 appears in the 08:00 bucket if that is when the sample landed. If n8n
queues and replays a batch of payloads after an outage, all of them are
stamped with the replay time, which is precisely the situation the
`mass-import` annotation exists to mark.

---

## ADR-010: In-Memory Cache for Live Status

**Status:** accepted

**Context.** `GET /coffee/status` reaches through to BSH via n8n. The status
widget is visible on every page and a user can refresh at will; the BSH quota
is a shared, limited resource.

**Decision.** A 7-second `IMemoryCache` entry in `CoffeeStatusController`,
plus `staleTime: Infinity` on the client query so it is fetched on demand
rather than polled.

**Consequences.** Bursts of requests cost one upstream call. Status can be up
to 7 seconds stale — irrelevant for "is the machine on". A failed read is also
cached, so an outage is reported consistently for 7 seconds rather than
flickering. After a power command the client waits ~3 s and then invalidates,
which is longer than the TTL and therefore reads fresh.

---

## ADR-011: Graceful Degradation on Status, Fail-Loud on Command

**Status:** accepted

**Context.** `HomeConnectService` handles both directions of the n8n
integration, and the two have different failure semantics.

**Decision.** `GetStatusAsync` catches every exception and returns a
`reachable: false` DTO with HTTP 200. `SetPowerStateAsync` calls
`EnsureSuccessStatusCode` and lets failures propagate to a 500.

**Rationale.** A failed *read* should not break the dashboard — "Offline" is
useful information. A failed *command* must never look like success; the user
would walk to a cold machine.

**Consequences.** `/coffee/status` never returns 5xx, so uptime monitoring on
that endpoint measures the API rather than the integration — `reachable` is
the field that matters. The asymmetry is intentional and is worth preserving
when the service is modified.

---

## ADR-012: Snapshot Services Split by Responsibility

**Status:** accepted

**Context.** `SnapshotService` had grown to 320 LOC behind a 9-member
interface and carried five unrelated concerns: ingest and idempotency, Home
Connect payload translation, plain queries, statistical aggregation, and
day-boundary arithmetic (TD-13). The range aggregation for `/api/stats/range`
lived in `StatsController` instead, next to a verbatim copy of the private
day-bounds method (TD-12, TD-14) — the same delta rule in two layers.

**Decision.** One type per concern, cut along the reason to change:

| Type | Concern |
|------|---------|
| `LocalDay` (Domain, static) | Local date + UTC offset → half-open UTC interval, and the reverse |
| `SnapshotPayloadMapper` (static) | Home Connect keys and `JsonElement` values → `MachineSnapshot` |
| `ISnapshotQueryService` | Which rows? No aggregation, no writes |
| `ISnapshotIngestService` | Idempotency gate and persistence |
| `ISnapshotStatisticsService` | Daily summary, range aggregation, heatmap |

`ISnapshotService` is gone; each caller depends on the interface it actually
uses. `StatsController` takes the query and statistics services, `IngestController`
the ingest service, `IngestWatchdog` the query service.

**Rationale.** The five groups changed for entirely different reasons — a new
Home Connect key touches the mapper, a new chart touches statistics, neither
should recompile or retest the other. Splitting also gave the range
aggregation a home in the service layer, which removed both the layering
violation and the duplicated day-bounds method: one behaviour, one definition.

**Alternatives rejected.** Keeping `ISnapshotService` as a delegating facade
would have preserved every call site, but the 9-member interface — the actual
finding — would have survived untouched. A coarser ingest/read split would
have left `JsonElement` parsing and heatmap bucketing in neighbouring types.

**Consequences.** Three DI registrations instead of one, and statistics depends
on the query service rather than on `AppDbContext` — except for the
`mass-import` lookup, which still reads `MarkedDays` directly. Test doubles get
cheaper: the watchdog's fake now implements 7 read methods instead of stubbing
an interface that also covered ingest and statistics.

---

## ADR-013: Bean-Hopper Overrides Keyed by Snapshot and Counter

**Status.** Accepted · #48

**Context.** The EQ900 has two bean hoppers, but Home Connect reports no hopper
— only per-category counters. Kaffee is drawn from hopper 1 and K+Milch from
hopper 2, so the assignment can be derived. It cannot always be derived
*correctly*: a plain coffee pulled from the espresso hopper looks identical in
the counters, so manual correction has to be possible.

A draw is not a stored row. Counters are cumulative, so a draw exists only as
the delta between two consecutive snapshots, and a 15-minute sampling interval
regularly contains several — sometimes across two counter columns at once.

**Decision.** Store corrections in `BeanHopperOverride`, keyed by
`(SnapshotId, Counter)`. The delta is attributed to the **later** of the two
snapshots, which makes the existing snapshot id a stable key: snapshots are
append-only and their ids are never rewritten. `BeanHopper` is nullable, and
`null` means "no bean consumption" — a third state, not a missing value.

Derivation stays computed rather than stored: no row means the default rule
applies. `source` in the response tells the caller which of the two it is
looking at.

**Alternatives rejected.** One override per snapshot — the shape the issue
first proposed — cannot express a mixed delta: correcting the two Kaffee in
`+2 Kaffee, +1 K+Milch` would drag the K+Milch along. A separate
delta/usage entity, materialised at ingest, would give overrides their own key,
but it duplicates data that is a subtraction away and needs backfilling for
every existing snapshot. Storing the derived hopper on the snapshot itself
would freeze the default rule into historical rows, so changing the rule would
need a data migration.

**Consequences.** A correction only makes sense where drinks were actually
drawn, so `SetOverrideAsync` rejects a counter with no delta at that snapshot —
without that guard a mistyped id would store an inert row. Deleting a snapshot
would cascade its overrides away and silently merge two deltas; nothing deletes
snapshots today. Grams, bean varieties and inventory stay out: this API reports
draws per hopper, the dashboard values them (Murgbyte/dashboard-s7#235).

