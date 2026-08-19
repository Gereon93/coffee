# 8. Cross-cutting Concepts

## 8.1 Domain Model

### 8.1.1 MachineSnapshot

A point-in-time reading of the machine's lifetime counters and status.
`CoffeeApi/Domain/MachineSnapshot.cs`.

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `int` | Auto-increment primary key |
| `Timestamp` | `DateTime` (UTC) | **Server receive time**, not the Home Connect event time. Indexed. |
| `MachineId` | `string` | Max 50, default `"EQ900-DEFAULT"`. Indexed. Never varied in practice. |
| `BeverageCounterCoffee` | `int` | Lifetime count |
| `BeverageCounterCoffeeAndMilk` | `int` | Lifetime count |
| `BeverageCounterMilk` | `int` | Lifetime count |
| `BeverageCounterHotWaterCups` | `int` | Lifetime count |
| `BeverageCounterHotWater` | `int` | Lifetime volume in **ml**, not a cup count |
| `OperationState` | `string` | Last segment of the BSH enum, e.g. `Ready` |
| `RemoteControlAllowed` | `bool` | |
| `LocalControlActive` | `bool` | |
| `InteriorIlluminationActive` | `bool` | |
| `CreatedAt` | `DateTime` (UTC) | Row creation time |
| `TotalBeverages` | `int` (computed) | `Coffee + CoffeeAndMilk + Milk + HotWaterCups`. EF-`Ignore`d. Deliberately excludes hot-water **ml**, which is not a beverage count. |

**Invariant (assumed, not enforced):** counters increase monotonically. The
machine can break this after a factory reset or mainboard replacement, and the
system has no defence — see [ADR-005](09-design.md#adr-005-counter-based-idempotency).

### 8.1.2 MarkedDay

A manual annotation on one local calendar date.
`CoffeeApi/Domain/MarkedDay.cs`.

| Property | Type | Notes |
|----------|------|-------|
| `Date` | `DateOnly` | Primary key, stored as a `yyyy-MM-dd` string |
| `Kind` | `string` | `mass-import` or `event`. Max 20, default `mass-import` |
| `EventType` | `string?` | Required for `event`: `birthday`, `visitors`, `party`, `sick`, `vacation`, `other`. Null for `mass-import` |
| `Reason` | `string` | Max 500. Required for `mass-import`, optional for `event` |
| `CreatedAt` | `DateTime` (UTC) | |

| Kind | Meaning | Effect on statistics |
|------|---------|---------------------|
| `mass-import` | Data backfilled in bulk; the timestamps do not reflect when coffee was actually brewed | Excluded from the heatmap; excluded from anomaly detection; row greyed out in the log |
| `event` | Real data that looks unusual for a known reason | Included in consumption totals and the heatmap; annotated in the bar chart. **Excluded from anomaly detection** — an explained day should not also be flagged as unexplained |

One annotation per date, enforced by the primary key. A day cannot be both
kinds — accepted, since the two are mutually exclusive in practice.

**Validation rules** (`MarkedDayService.CreateAsync`), each with its own error
code and HTTP status:

| Rule | Error | Status |
|------|-------|--------|
| Date parses as `yyyy-MM-dd` | `InvalidDate` | 400 |
| Kind ∈ {`mass-import`, `event`} | `InvalidKind` | 400 |
| `event` requires a valid `eventType` | `InvalidEventType` | 400 |
| `mass-import` requires a non-blank reason | `ReasonRequired` | 400 |
| Date not already annotated | `AlreadyMarked` | 409 |
| Date exists (delete) | `NotFound` | 404 |

## 8.2 Idempotency

`POST /api/ingest` is idempotent by construction: a row is written only if at
least one *beverage* counter is strictly greater than in the latest stored
snapshot for the same machine.

```
write ⟺  new.Coffee          > last.Coffee
      ∨  new.CoffeeAndMilk   > last.CoffeeAndMilk
      ∨  new.Milk            > last.Milk
      ∨  new.HotWaterCups    > last.HotWaterCups
```

Consequences worth stating explicitly:

- Replaying a payload N times yields exactly one row and N identical
  `200 OK` responses carrying the *stored* snapshot's id.
- `BeverageCounterHotWater` (ml) is **not** in the predicate. Drawing hot water
  without a cup count change writes nothing.
- Status-only changes are never persisted after the first snapshot. The
  `OperationState` shown in the log is the state at the moment of the last
  *consumption*, not the current state. Live state comes from
  `/coffee/status` instead.
- A counter reset is indistinguishable from "nothing happened", so it is never
  recorded and the baseline stays at the pre-reset maximum. Deltas then clamp
  to 0 until the counters climb past the old high.

A composite index `(MachineId, Coffee, CoffeeAndMilk, Milk)` exists on
`MachineSnapshots`. The current implementation does not query on that shape —
it fetches the single latest row by timestamp — so the index earns nothing
today.

## 8.3 Time and Timezone Handling

### Storage

- Every persisted `DateTime` is UTC, produced by `DateTime.UtcNow`.
- SQLite has no `DateTimeKind`, so values read back would default to
  `Unspecified` and serialise **without** a `Z` — the browser would then read
  them as local time. `AppDbContext.OnModelCreating` prevents this by applying
  a global `ValueConverter` to every `DateTime` / `DateTime?` property that
  re-stamps `DateTimeKind.Utc` on read.

### Query

- The frontend computes its offset once per module load:
  `const TZ = -new Date().getTimezoneOffset()` (`src/api/stats.ts`), giving
  `+60` for CET and `+120` for CEST, and appends `tz=<minutes>` to every
  time-aware request.
- The backend converts a local date to a half-open UTC interval:

  ```csharp
  start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(-tzOffsetMinutes);
  end   = start.AddDays(1);
  ```

- Grouping keys use `timestamp.AddMinutes(tz)`: peak hour, heatmap
  `(dayOfWeek, hour)`, and range grouping by local date.
- The heatmap emits ISO-8601 weekday numbering (Monday = 1 … Sunday = 7),
  converted from .NET's `DayOfWeek` where Sunday = 0.

### Display

- The dashboard renders timestamps with `toLocaleString('de-DE', …)` and
  formats dates as `dd.MM.yyyy`.
- `coffeeTimeLock.ts` is the one place that uses a real IANA zone: it asks
  `Intl.DateTimeFormat` for the current hour in `Europe/Berlin`, so the power
  window holds even for a client whose machine clock is set elsewhere.

### Known limitation — a fixed offset is not a timezone

A single offset is applied to every date in a request. A 52-week heatmap
requested during CEST applies `+120` to samples recorded during CET, shifting
their local midnight by an hour and moving late-evening samples into the wrong
day and hour bucket. Accepted for a single-timezone household; the correct fix
is to pass the zone id and use `TimeZoneInfo`. See
[ADR-004](09-design.md#adr-004-client-driven-timezone-offset).

## 8.4 Security

### Threat model

The system holds no accounts and no identifiers, and no credentials of value
beyond the n8n webhook basic-auth pair and the ingest API key. It is not
entirely free of personal data: `MarkedDay.Reason` is user-entered free text.
The realistic threats are (a) accidental exposure of the n8n webhook,
(b) unwanted actuation of a physical appliance, and (c) user-entered notes
ending up somewhere they were not expected.

### Controls in place

| Concern | Control | Where |
|---------|---------|-------|
| Write authentication | `X-API-Key` header on `/api/ingest`, `POST /coffee/power` and `POST` / `DELETE /api/stats/marked-days`, compared with `CryptographicOperations.FixedTimeEquals`. The route table is method-aware, so reads on `/api/stats/marked-days` and `/coffee/status` stay open | `ApiKeyMiddleware` |
| Key delivery to the dashboard | The dashboard's nginx injects `X-API-Key` from the `API_KEY` container variable, so the secret never enters the browser bundle | `nginx.conf.template` |
| Outbound webhook auth | Optional HTTP Basic on the n8n webhook | `HomeConnectService` |
| Secret handling | `appsettings.Secrets.json` (gitignored) and environment variables; no secrets in source | `Program.cs` |
| Error tracking privacy | `SendDefaultPii = false` on backend and frontend | `Program.cs`, `lib/sentry.ts` |
| Information disclosure | Controllers return generic 500 bodies; exception detail goes to logs and GlitchTip only | all controllers |
| Network exposure | LAN-only deployment; no inbound internet path | Deployment |
| Actuation throttling | Fixed window of 10 `POST /coffee/power` calls per minute, ahead of the API-key check; beyond it the API answers `429` | `Program.cs` |
| API documentation exposure | Scalar and `/openapi/v1.json` are mapped in `Development` only and are not proxied by nginx | `Program.cs`, `nginx.conf.template` |
| Caller address in the logs | `X-Forwarded-For` is honoured only from the proxy networks named in `ForwardedHeaders:KnownNetworks` | `Program.cs` |
| Accidental actuation | UI time lock, 07:00–18:00 Europe/Berlin | `coffeeTimeLock.ts` |

### Residual risks — stated plainly

| Gap | Reality |
|-----|---------|
| **The dashboard origin is an unauthenticated path to the writes** | The write endpoints now require the API key, which closes direct calls to the API port. The dashboard's nginx injects the key for everyone it serves, so anything that can reach the dashboard port can still actuate the machine. That is the same reach as pressing the button in the UI, which is the intended feature — closing it needs real user authentication, not a shared secret. The 07:00–18:00 lock in `coffeeTimeLock.ts` does not help: it is client-side. |
| **Missing `ApiKey` disables write auth** | The middleware logs a warning and forwards the request. A configuration mistake in production silently removes authentication on every protected endpoint instead of failing loudly. |
| **Read endpoints are unauthenticated** | Every `GET` is open to anything that reaches the API port. Consumption counters are the only data at stake, and the LAN-only assumption is what carries this. |
| **Rate limiting covers actuation only** | `POST /coffee/power` is throttled; the read endpoints are not. They hit local SQLite, so the exposure is CPU, not a third-party quota. |
| **Forwarded headers are off unless configured** | Without `ForwardedHeaders:KnownNetworks` the logged address stays the proxy's. Configuring it on a deployment where the API port is directly reachable trades one wrong address for a spoofable one — which is why it is opt-in. |
| **User-entered text reaches the logs** | `MarkedDayService.CreateAsync` writes `Reason` verbatim into an information-level log line. It is free text up to 500 characters and can contain names. `SendDefaultPii = false` governs framework-collected data, not application-supplied log values, so it does not help here. No retention policy covers the logs. |

These are consequences of the LAN-only assumption (TC-3). They become
release-blocking the moment the deployment model changes. Tracked in
[11](11-risks.md).

## 8.5 Error Handling

| Layer | Strategy |
|-------|----------|
| **Middleware** | Auth failures short-circuit with 401 and a JSON body; the attempt is logged with the remote address |
| **Controller** | Request-shape validation returns 400 with `{ error, details[] }`. Domain errors are mapped from a service-level error enum to 400/404/409. Unexpected exceptions are caught, logged, and answered with a generic 500 — never `ex.Message` |
| **Service** | Domain services return typed results (`MarkedDayError`) rather than throwing for expected failures. Infrastructure failures propagate |
| **`HomeConnectService`** | Asymmetric by design: `SetPowerStateAsync` propagates (a failed command must be visible), `GetStatusAsync` swallows everything and returns `reachable: false` (a failed read should degrade, not error) |
| **Frontend transport** | `fetchJson` throws a typed `ApiError` carrying the HTTP status |
| **Frontend UI** | `isError` renders `<ErrorMessage />`; TanStack Query retries once, and not at all for the status query |
| **Tracking** | Sentry SDK on both sides reporting to a self-hosted GlitchTip, tagged `service=coffee-api` / `service=coffee-dashboard` |

> The frontend has **no React error boundary**. A render-time exception in a
> chart unmounts the page to a blank screen rather than a contained error
> state. Recorded in [11](11-risks.md).

## 8.6 Persistence

| Aspect | Approach |
|--------|----------|
| ORM | EF Core 9 with the SQLite provider |
| Schema evolution | Code-first migrations, applied at startup, with `MigrationBaseliner` for pre-migration databases |
| Indexes | `Timestamp`, `MachineId`, composite idempotency index, `MarkedDay.Date` (PK) |
| Type mapping | `DateOnly ↔ "yyyy-MM-dd"` string; global `DateTime → Utc` read converter |
| Query style | Async throughout; projection to DTOs inside the query where possible (`MarkedDayService.GetAllAsync`) |
| Concurrency | Single writer (ingest). No optimistic concurrency tokens, no transactions beyond the implicit `SaveChangesAsync` unit of work |
| Migrations to date | `Initial` → `AddExcludedDays` → `RenameExcludedDaysToMarkedDays` |

Note that aggregation for daily summaries, ranges, and the heatmap is done
**in memory** after materialising the relevant snapshots. At the current data
volume (~76 rows/day) that is the simpler and faster choice; it would need
revisiting only if the dataset grew by orders of magnitude.

## 8.7 Testing

| Category | Framework / technique | Scope |
|----------|----------------------|-------|
| Unit | xUnit + EF Core InMemory | The snapshot services (payload mapping, queries, idempotency, daily summary, range, heatmap), `MarkedDayService`, domain computation |
| Service with I/O | `StubHttpMessageHandler` | `HomeConnectService`: success, non-2xx, timeout, unparsable body |
| Controller | xUnit + InMemory context | Every action, including every validation and error branch |
| Infrastructure | Real temporary SQLite files | `MigrationBaseliner`: fresh DB, legacy DB, already-baselined DB |
| Integration | `WebApplicationFactory` + real SQLite | Full HTTP pipeline: routing, middleware, API-key enforcement, migrations |
| Frontend | ESLint + `tsc -b` | Compile-time only |

**Current state: 87 backend tests and 102 dashboard tests, all passing.**

Test data is constructed via `SnapshotBuilder` so tests state only the values
they care about.

> **Gap:** the frontend has no runtime tests. `anomalyUtils`, `dateUtils`,
> `markedDayUtils`, and `coffeeTimeLock` are pure functions with real logic
> (z-scores, week boundaries, a timezone-sensitive window) and no coverage.
> Recorded in [11](11-risks.md).

## 8.8 Observability

| Signal | Backend | Frontend |
|--------|---------|----------|
| Logs | `ILogger<T>` with structured properties; ingest skips at `Debug`, snapshot creation and auth failures at `Information`/`Warning` | Browser console |
| Errors | Sentry SDK → GlitchTip, `AttachStacktrace = true`, PII off | `@sentry/react` → GlitchTip |
| Traces | `SENTRY_TRACES_SAMPLE_RATE`, default `0.0` | `VITE_SENTRY_TRACES_SAMPLE_RATE`, default `0` |
| Health | `GET /api/health` with database connectivity and `lastSnapshot` | — |
| Build provenance | `SENTRY_RELEASE` | `__BUILD_COMMIT__` / `__BUILD_TIME__` inlined at build |

`lastSnapshot` is the most useful operational signal in the system: if it is
hours old, the n8n workflow has stopped — the API itself will look perfectly
healthy.

## 8.9 User Interface Concepts

| Concept | Realisation |
|---------|-------------|
| Language | German labels throughout |
| Theme | Tailwind CSS v4, dark mode via system preference, coffee-toned palette |
| Layout | Mobile-first; charts reflow via Recharts `ResponsiveContainer` |
| Loading / error | Shared `LoadingSpinner` and `ErrorMessage` per data region, not per page |
| Server state | TanStack Query: `retry: 1`, no refetch on window focus; the status query is on-demand only (`staleTime: Infinity`, `retry: 0`) |
| Time period | `week` / `month` / `year` / `all`, weeks starting Monday (`date-fns`) |
| Anomalies | Z-score over the selected range, threshold 1.5 σ, mass-import days excluded before the statistics are computed |
| Interaction | Click a bar to annotate that day; annotate or un-annotate directly from the log table |
| Safety | Power button disabled outside 07:00–18:00 Europe/Berlin |
