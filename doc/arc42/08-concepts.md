# 8. Cross-cutting Concepts

## 8.1 Domain Model

### MachineSnapshot

The core entity representing a point-in-time reading of the coffee machine's
counters and status.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Auto-increment primary key |
| Timestamp | DateTime (UTC) | When the snapshot was ingested |
| MachineId | string | Machine identifier (always `EQ900-DEFAULT`) |
| BeverageCounterCoffee | int | Lifetime coffee counter |
| BeverageCounterCoffeeAndMilk | int | Lifetime coffee+milk counter |
| BeverageCounterMilk | int | Lifetime milk counter |
| BeverageCounterHotWaterCups | int | Lifetime hot water cups counter |
| BeverageCounterHotWater | int | Lifetime hot water in ml |
| OperationState | string | Machine state (Ready, Brewing, etc.) |
| RemoteControlAllowed | bool | Remote control enabled |
| LocalControlActive | bool | Local control active |
| InteriorIlluminationActive | bool | Interior light on |
| CreatedAt | DateTime (UTC) | Record creation timestamp |
| **TotalBeverages** | **int** | **Computed**: Coffee + CoffeeAndMilk + Milk + HotWaterCups |

### MarkedDay

An annotation for a specific date. Two kinds:

| Kind | Semantics | Effect on statistics |
|------|-----------|---------------------|
| `mass-import` | Backfilled data; unreliable | Excluded from heatmap and anomaly detection |
| `event` | Valid data with explanation (birthday, visitors, etc.) | Included in statistics; annotated in charts |

| Property | Type | Description |
|----------|------|-------------|
| Date | DateOnly | Primary key; local date (yyyy-MM-dd) |
| Kind | string | `mass-import` or `event` |
| EventType | string? | Required for events: birthday, visitors, party, sick, vacation, other |
| Reason | string | Free-text note; required for mass-import |
| CreatedAt | DateTime (UTC) | When the annotation was created |

## 8.2 Idempotency

The ingest endpoint is idempotent: a new snapshot is only persisted when at
least one beverage counter has increased compared to the latest snapshot.
This prevents duplicate records when n8n re-delivers the same payload.

**Rule:** `NEW.counter > OLD.counter` for any of: Coffee, CoffeeAndMilk,
Milk, HotWaterCups. Status-only changes (OperationState, etc.) do not
trigger a new snapshot.

## 8.3 Timezone Handling

- All timestamps are stored as UTC (`DateTimeKind.Utc`) in SQLite.
- A global EF Core `ValueConverter` forces `DateTimeKind.Utc` on read,
  compensating for SQLite's loss of kind information.
- The frontend sends its UTC offset via the `tz` query parameter (in
  minutes, e.g. 60 for CET, 120 for CEST).
- The backend computes local day boundaries as
  `local_midnight - tz_offset` in UTC.
- Peak hour and heatmap grouping use the client's local time.

## 8.4 Security

| Concern | Measure |
|---------|---------|
| Ingest authentication | API key via `X-API-Key` header; configured via `ApiKey` env var |
| Power control | No authentication (LAN-only deployment); UI-level time lock (07:00-18:00 Berlin) |
| Read endpoints | No authentication (LAN-only) |
| CORS | Allow all (LAN-only; no cross-origin concerns) |
| Secrets | n8n credentials in `appsettings.Secrets.json` (gitignored); API key in env var |
| Error tracking | `SendDefaultPii = false`; no PII in Sentry events |

## 8.5 Error Handling

| Layer | Strategy |
|-------|----------|
| **Controller** | Catch-all `try/catch` returns 500 with error message; validation returns 400 |
| **Service** | Throws on unexpected state; logs with structured context |
| **HomeConnectService** | Catches timeouts and network errors; returns "unreachable" DTO instead of throwing |
| **Frontend** | `ApiError` class with HTTP status; `ErrorMessage` component for user feedback |
| **Error tracking** | GlitchTip/Sentry integration in both backend and frontend |

## 8.6 Testing

| Category | Framework | Scope |
|----------|-----------|-------|
| Unit tests | xUnit | Services, domain entities, utilities |
| Controller tests | xUnit + InMemory DB | Every controller branch |
| Integration tests | `WebApplicationFactory` + real SQLite | Full HTTP pipeline, migrations, auth |
| Frontend | ESLint + TypeScript strict | Compile-time safety; no runtime tests |

Test database: EF Core `InMemoryDatabase` for unit/controller tests;
temporary SQLite file for integration and migration tests.
