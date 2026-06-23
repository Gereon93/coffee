# 5. Building Block View

## 5.1 Level 1: System Decomposition

```
┌──────────────────────────────────────────────────────────────────────┐
│                        Coffee Analytics Hub                          │
│                                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │  CoffeeApi   │  │  CoffeeTest  │  │ coffee-      │              │
│  │  (ASP.NET)   │  │  (xUnit)     │  │ dashboard    │              │
│  │              │  │              │  │ (React/Vite) │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
│                                                                      │
│  ┌──────────────┐  ┌──────────────┐                                 │
│  │  build.sh    │  │  .github/    │                                 │
│  │  (Docker)    │  │  (CI/CD)     │                                 │
│  └──────────────┘  └──────────────┘                                 │
└──────────────────────────────────────────────────────────────────────┘
```

| Building Block | Responsibility |
|----------------|----------------|
| **CoffeeApi** | ASP.NET Core Web API; ingest, statistics, power control, health check |
| **CoffeeTest** | xUnit test suite; unit tests, integration tests, service tests |
| **coffee-dashboard** | React SPA; charts, KPI cards, heatmap, log view, power toggle |
| **build.sh** | Local Docker build and push script (Podman/Docker) |
| **.github/** | GitHub Actions workflows for CI and Docker image publishing |

## 5.2 Level 2: CoffeeApi Internal Structure

```
CoffeeApi/
├── Controllers/
│   ├── IngestController.cs         # POST /api/ingest
│   ├── StatsController.cs          # GET /api/stats/*
│   ├── MarkedDaysController.cs     # CRUD /api/stats/marked-days
│   ├── PowerController.cs          # POST /coffee/power
│   └── CoffeeStatusController.cs   # GET /coffee/status
├── Domain/
│   ├── MachineSnapshot.cs          # Core entity: machine state at a point in time
│   └── MarkedDay.cs                # Annotation entity: mass-import or event
├── DTOs/
│   ├── IngestPayloadDto.cs         # n8n input payload
│   ├── SnapshotResponseDto.cs      # Snapshot API output + pagination wrappers
│   ├── MarkedDayDto.cs             # MarkedDay API output + creation input
│   ├── PowerRequestDto.cs          # Power on/off request
│   └── CoffeeStatusDto.cs          # Live machine status output
├── Infrastructure/
│   ├── AppDbContext.cs             # EF Core DbContext, model configuration
│   └── MigrationBaseliner.cs       # Pre-migration DB compatibility
├── Middleware/
│   └── ApiKeyMiddleware.cs         # API key authentication for /api/ingest
├── Services/
│   ├── ISnapshotService.cs         # Snapshot service interface
│   ├── SnapshotService.cs          # Idempotency, queries, aggregation
│   ├── IHomeConnectService.cs      # Home Connect relay interface
│   └── HomeConnectService.cs       # n8n webhook communication
├── Migrations/                     # EF Core migration files
├── Program.cs                      # Application entry point, DI, middleware
└── appsettings.json                # Configuration
```

### White Box: Key Components

**IngestController** — Validates incoming payloads, delegates to
`SnapshotService.ProcessIngestAsync()`. Returns 201 for new snapshots,
200 for duplicates, 400 for invalid payloads.

**StatsController** — Read-only endpoints for statistics. Delegates to
`SnapshotService` for queries. Directly accesses `AppDbContext` for
baseline snapshot lookups (cross-day delta support).

**SnapshotService** — Core business logic. Handles idempotency checks
(counter comparison), timezone-aware day boundary computation, daily
summaries with peak hour detection, heatmap aggregation with mass-import
exclusion, and Home Connect key-to-entity mapping.

**MarkedDaysController** — CRUD for day annotations. Bypasses the service
layer and accesses `AppDbContext` directly. Supports two kinds:
`mass-import` (excluded from statistics) and `event` (annotated but
included in statistics).

**HomeConnectService** — Communicates with n8n webhooks for power control
(HTTP PUT) and live status retrieval. Handles timeouts and network errors
gracefully, returning an "unreachable" status instead of throwing.

**ApiKeyMiddleware** — Protects `/api/ingest` with a shared secret
(`X-API-Key` header). Falls through to an open endpoint when no key is
configured (development mode).

**MigrationBaseliner** — Detects pre-migration databases and seeds the
EF Core migration history table so that `Database.Migrate()` does not
attempt to re-create existing tables.

## 5.3 Level 2: coffee-dashboard Internal Structure

```
coffee-dashboard/src/
├── api/
│   ├── client.ts          # Generic fetch wrapper with error handling
│   ├── coffee.ts          # Power control + status API calls
│   ├── stats.ts           # Statistics API calls (daily, range, heatmap, etc.)
│   └── types.ts           # TypeScript types mirroring backend DTOs
├── components/
│   ├── anomaly/           # AnomalyBadge
│   ├── cards/             # KpiCard, KpiCardGrid
│   ├── charts/            # DailyBarChart, TrendLineChart, ConsumptionPieChart,
│   │                      # HourlyPeaksChart, HeatmapGrid, WeekdayComparisonChart
│   ├── controls/          # TimePeriodSelector
│   ├── dashboard/         # MarkDayEventModal
│   ├── layout/            # AppShell, NavBar, CoffeePowerButton
│   ├── log/               # MarkAsBackfillModal
│   └── shared/            # ErrorMessage, LoadingSpinner
├── hooks/                 # TanStack React Query hooks for each API endpoint
├── lib/                   # Utilities: date, formatters, anomaly detection, sentry
└── pages/
    ├── DashboardPage.tsx  # Main dashboard with all charts
    ├── HeatmapPage.tsx    # Full-page heatmap with week selector
    └── LogPage.tsx        # Paginated snapshot table with annotation controls
```
