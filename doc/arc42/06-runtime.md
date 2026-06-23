# 6. Runtime View

## 6.1 Ingest Sequence

```
n8n                    IngestController    SnapshotService       AppDbContext      SQLite
  │                         │                    │                    │               │
  │ POST /api/ingest        │                    │                    │               │
  │ + X-API-Key header      │                    │                    │               │
  │────────────────────────►│                    │                    │               │
  │                         │  ApiKeyMiddleware  │                    │               │
  │                         │  validates key     │                    │               │
  │                         │                    │                    │               │
  │                         │  ProcessIngestAsync(payload)            │               │
  │                         │───────────────────►│                    │               │
  │                         │                    │  MapToEntity()     │               │
  │                         │                    │  (parse HC keys)   │               │
  │                         │                    │                    │               │
  │                         │                    │  GetLatestAsync()  │               │
  │                         │                    │───────────────────►│  SELECT ...    │
  │                         │                    │                    │─────────────►│
  │                         │                    │                    │◄─────────────│
  │                         │                    │◄───────────────────│  lastSnapshot │
  │                         │                    │                    │               │
  │                         │                    │  HasCounterIncreased()?            │
  │                         │                    │  ──► YES           │               │
  │                         │                    │                    │               │
  │                         │                    │  Add(snapshot)     │               │
  │                         │                    │───────────────────►│  INSERT        │
  │                         │                    │                    │─────────────►│
  │                         │                    │◄───────────────────│               │
  │                         │                    │                    │               │
  │                         │◄───────────────────│  (true, snapshot)  │               │
  │                         │                    │                    │               │
  │  201 Created            │                    │                    │               │
  │◄────────────────────────│                    │                    │               │
```

## 6.2 Daily Statistics Sequence

```
Dashboard          StatsController     SnapshotService      AppDbContext       SQLite
  │                      │                    │                   │               │
  │ GET /api/stats/daily │                    │                   │               │
  │ /2026-01-25?tz=60   │                    │                   │               │
  │─────────────────────►│                    │                   │               │
  │                      │  GetByDateAsync()  │                   │               │
  │                      │───────────────────►│                   │               │
  │                      │                    │  Compute UTC      │               │
  │                      │                    │  day boundaries   │               │
  │                      │                    │──────────────────►│  SELECT ...    │
  │                      │                    │                   │─────────────►│
  │                      │                    │◄──────────────────│               │
  │                      │◄───────────────────│  List<Snapshot>   │               │
  │                      │                    │                   │               │
  │                      │  GetDailySummaryAsync()                │               │
  │                      │───────────────────►│                   │               │
  │                      │                    │  Get previous     │               │
  │                      │                    │  day's last snap  │               │
  │                      │                    │──────────────────►│  SELECT ...    │
  │                      │                    │                   │─────────────►│
  │                      │                    │◄──────────────────│  baseline     │
  │                      │                    │                   │               │
  │                      │                    │  Compute deltas   │               │
  │                      │                    │  Find peak hour   │               │
  │                      │◄───────────────────│  DailySummaryDto  │               │
  │                      │                    │                   │               │
  │                      │  Prepend baseline  │                   │               │
  │                      │  to snapshot list  │                   │               │
  │                      │                    │                   │               │
  │  200 OK              │                    │                   │               │
  │◄─────────────────────│                    │                   │               │
```

## 6.3 Power Control Sequence

```
Dashboard       PowerController    HomeConnectService     n8n         BSH Cloud
  │                   │                   │                  │              │
  │ POST /coffee/power                    │                  │              │
  │ { state: "on" }   │                   │                  │              │
  │──────────────────►│                   │                  │              │
  │                   │  Validate state   │                  │              │
  │                   │  ("on" or "off")  │                  │              │
  │                   │                   │                  │              │
  │                   │  SetPowerStateAsync(true)            │              │
  │                   │──────────────────►│                  │              │
  │                   │                   │  PUT webhook     │              │
  │                   │                   │─────────────────►│              │
  │                   │                   │                  │  PUT /homestatus
  │                   │                   │                  │─────────────►│
  │                   │                   │                  │              │
  │                   │                   │◄─────────────────│              │
  │                   │◄──────────────────│                  │              │
  │                   │                   │                  │              │
  │  200 OK           │                   │                  │              │
  │◄──────────────────│                   │                  │              │
```

## 6.4 Startup / Migration Sequence

```
Program.cs              MigrationBaseliner        AppDbContext          SQLite
  │                          │                         │                   │
  │  App startup             │                         │                   │
  │─────────────────────────►│                         │                   │
  │                          │  Check MachineSnapshots │                   │
  │                          │  table exists?          │                   │
  │                          │────────────────────────►│  PRAGMA table_info│
  │                          │                         │─────────────────►│
  │                          │◄────────────────────────│                   │
  │                          │                         │                   │
  │                          │  Check __EFMigrations   │                   │
  │                          │  History exists?        │                   │
  │                          │────────────────────────►│                   │
  │                          │◄────────────────────────│  NO               │
  │                          │                         │                   │
  │                          │  Seed history row       │                   │
  │                          │  (Initial migration)    │                   │
  │                          │────────────────────────►│  INSERT           │
  │                          │                         │─────────────────►│
  │                          │◄────────────────────────│                   │
  │                          │                         │                   │
  │  Database.Migrate()      │                         │                   │
  │──────────────────────────────────────────────────►│  Apply pending    │
  │                          │                         │  migrations       │
  │                          │                         │─────────────────►│
  │◄──────────────────────────────────────────────────│                   │
```
