# Graph Report - coffee  (2026-10-01)

## Corpus Check
- 192 files · ~246,639 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: (none) 9, .example 1, .props 1)

## Summary
- 1946 nodes · 4554 edges · 98 communities (93 shown, 5 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 413 edges (avg confidence: 0.88)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `b0e416f`
- Run `git diff b0e416f --stat -- ':!graphify-out' ':!.graphify*' ':!AGENTS.md' ':!CLAUDE.md'` to check if indexed source files have changed since the build.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Create
- .Map
- BeanHopperService
- IngestWatchdog
- StatsControllerTests
- HistoricalBackfillService
- Recording Test Logger
- types.ts
- DailyBarChart.tsx
- Factory
- CoffeeApi.Services
- .EnsureBaselined
- components.test.tsx
- HomeConnect Test Stubs
- layout.test.tsx
- ApiIntegrationTests
- CoffeeApi.Infrastructure
- Launch Settings
- DashboardPage.tsx
- Marked Days Tests
- MachineSnapshot
- charts.test.tsx
- MigrationBaseliner
- TS App Config
- CoffeeTest.Helpers
- package.json
- Frontend Dev Dependencies
- ApiIntegrationTests.cs
- react
- TS Node Config
- CoffeeTest.csproj
- Marked Days Controller
- Marked Day Service
- CoffeeStatusDto
- SnapshotResponseDto.cs
- ApiKeyMiddleware
- StatsController
- arc42/README.md
- .BoundsUtc
- SnapshotStatisticsService
- Snapshot Query Service
- CoffeeStatusController
- Snapshot Query Interface
- vite.config.ts
- API Specification: Coffee Analytics Hub
- HomeConnectService
- Frontend Dependencies
- Marked Day Entity
- NPM Scripts
- SnapshotResponseDto
- Marked Day DTO
- IngestPayloadValidatorTests
- Coffee Analytics Hub
- AddBeanHopperOverrides
- UnreachableSnapshotQueryService
- SentryErrorBoundary.test.tsx
- 8. Cross-cutting Concepts
- .Main
- Create Marked Day DTO
- DbContext Model Snapshot
- Build & Push Script
- Test Setup
- TS Root Config
- IngestControllerTests
- CollectingLogger
- Endpoints
- pages.test.tsx
- .Ingest
- 4. Solution Strategy
- backup.sh
- test-backup.sh
- AppDbContext
- .Ingest
- IngestDataDto
- 9. Design Decisions
- Review guidelines
- EstimatedSnapshotFlagMigrationTests
- Program.cs
- PaginatedResponseDto
- SqliteOutageHelper
- CLAUDE.md
- DropUnusedIdempotencyIndex
- 5.2 Level 2 — CoffeeApi
- Correctness
- entrypoint.sh
- Coffee Dashboard
- InvalidIngestPayloadException
- 11. POST /api/stats/snapshots/{id}/bean-hopper
- 1. POST /api/ingest
- 5. GET /api/stats/daily/{date}
- 10. DELETE /api/stats/snapshots/{id}/bean-hopper
- 2. GET /api/stats
- 7. GET /api/stats/heatmap
- n8n Workflow Spezifikation
- test-docker-backup.sh
- 13. GET /api/health
- 9. POST /api/stats/marked-days

## God Nodes (most connected - your core abstractions)
1. `SnapshotBuilder` - 86 edges
2. `MachineSnapshot` - 80 edges
3. `AppDbContext` - 47 edges
4. `CoffeeApi.Services` - 44 edges
5. `CoffeeApi.DTOs` - 41 edges
6. `ApiIntegrationTests` - 32 edges
7. `CoffeeApi.Domain` - 29 edges
8. `CoffeeApi.Infrastructure` - 28 edges
9. `SetBeanHopperDto` - 27 edges
10. `BeanHopperServiceTests` - 27 edges

## Surprising Connections (you probably didn't know these)
- `2.3 Conventions` --references--> `StatsController`  [INFERRED]
  doc/arc42/02-constraints.md → CoffeeApi/Controllers/StatsController.cs
- `3.3.4 Status flow (Dashboard → API → n8n)` --references--> `CoffeeStatusDto`  [INFERRED]
  doc/arc42/03-context.md → CoffeeApi/DTOs/CoffeeStatusDto.cs
- `P1 — should fix` --references--> `AppDbContext`  [INFERRED]
  AGENTS.md → CoffeeApi/Infrastructure/AppDbContext.cs
- `Security` --references--> `ApiKeyMiddleware`  [INFERRED]
  doc/arc42/11-risks.md → CoffeeApi/Middleware/ApiKeyMiddleware.cs
- `Configuration` --references--> `HomeConnectService`  [INFERRED]
  doc/arc42/07-deployment.md → CoffeeApi/Services/HomeConnectService.cs

## Import Cycles
- None detected.

## Communities (98 total, 5 thin omitted)

### Community 0 - ".Create"
Cohesion: 0.06
Nodes (42): SetBeanHopperDto, BeanHopper, Counter, BeanHoppersControllerTests, BadRequestObjectResult, DateTime, Fact, List (+34 more)

### Community 1 - ".Map"
Cohesion: 0.19
Nodes (9): SnapshotPayloadMapper, DateTime, SnapshotPayloadMapperTests, DateTime, Fact, InlineData, JsonElement, Theory (+1 more)

### Community 2 - "BeanHopperService"
Cohesion: 0.06
Nodes (41): BeanHoppersController, HttpDelete, HttpPost, IActionResult, ProducesResponseType, Task, BeanCounters, BeanHopperOverride (+33 more)

### Community 3 - "IngestWatchdog"
Cohesion: 0.07
Nodes (34): BackgroundService, IngestWatchdog, CancellationToken, ILogger, IServiceScopeFactory, LoggerMessage, Task, TimeProvider (+26 more)

### Community 4 - "StatsControllerTests"
Cohesion: 0.28
Nodes (7): StatsControllerTests, BadRequestObjectResult, Fact, InlineData, OkObjectResult, Task, Theory

### Community 5 - "HistoricalBackfillService"
Cohesion: 0.07
Nodes (37): HistoricalBackfillController, HttpPost, IActionResult, ProducesResponseType, Task, HistoricalBackfillDefaults, HistoricalBackfillPlanDto, AlreadyApplied (+29 more)

### Community 6 - "Recording Test Logger"
Cohesion: 0.09
Nodes (25): RecordingLogger, Entries, EventId, Exception, Func, IDisposable, IReadOnlyList, List (+17 more)

### Community 7 - "types.ts"
Cohesion: 0.12
Nodes (22): ApiError, fetchJson(), addMarkedDay(), fetchHealth(), fetchLatestSnapshot(), fetchMarkedDays(), fetchRange(), removeMarkedDay() (+14 more)

### Community 8 - "DailyBarChart.tsx"
Cohesion: 0.14
Nodes (18): DailyAggregate, MarkedDay, ChartEntry, DailyBarChart(), EventBadges(), EventBadgesProps, Props, RechartsInternals (+10 more)

### Community 9 - "Factory"
Cohesion: 0.07
Nodes (30): Action, Program, TimeSpan, CollectingLogger, CollectingLoggerProvider, Factory, ConfigureApiKey, LogMessages (+22 more)

### Community 10 - "CoffeeApi.Services"
Cohesion: 0.14
Nodes (9): CoffeeApi.Domain, CoffeeApi.Controllers, CoffeeTest.Domain, CoffeeApi.DTOs, CoffeeApi.Services, CoffeeTest.Controllers, microsoft_aspnetcore_mvc, microsoft_extensions_caching_memory (+1 more)

### Community 11 - ".EnsureBaselined"
Cohesion: 0.19
Nodes (9): ILogger, LoggerMessage, MigrationBaselinerTests, Fact, DbConnection, IMigrationsAssembly, Neue Migration anlegen, Schema-Migrationen (+1 more)

### Community 12 - "components.test.tsx"
Cohesion: 0.12
Nodes (24): DailySummary, SnapshotResponse, AnomalyBadge(), Props, KpiCard(), Props, KpiCardGrid(), periodLabels (+16 more)

### Community 13 - "HomeConnect Test Stubs"
Cohesion: 0.13
Nodes (20): StubHttpMessageHandler, CallCount, LastRequest, LastRequestBody, CancellationToken, Exception, Func, Task (+12 more)

### Community 14 - "layout.test.tsx"
Cohesion: 0.10
Nodes (25): fetchCoffeeStatus(), setCoffeePower(), CoffeeStatus, queryClient, AppShell(), getInitialTheme(), MockErrorBoundary, Theme (+17 more)

### Community 15 - "ApiIntegrationTests"
Cohesion: 0.15
Nodes (11): CoffeeApiFactory, ApiIntegrationTests, CoffeeApiFactory, DbPath, Fact, HttpClient, IWebHostBuilder, JsonElement (+3 more)

### Community 16 - "CoffeeApi.Infrastructure"
Cohesion: 0.20
Nodes (10): CoffeeApi.Migrations, CoffeeTest.Infrastructure, CoffeeApi.Infrastructure, microsoft_data_sqlite, microsoft_entityframeworkcore, microsoft_entityframeworkcore_design, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations (+2 more)

### Community 17 - "Launch Settings"
Cohesion: 0.07
Nodes (28): commandName, environmentVariables, launchBrowser, launchUrl, publishAllPorts, ASPNETCORE_ENVIRONMENT, ASPNETCORE_HTTP_PORTS, applicationUrl (+20 more)

### Community 18 - "DashboardPage.tsx"
Cohesion: 0.12
Nodes (27): fetchDaily(), fetchSnapshots(), App(), Props, TrendLineChart(), options, Props, TimePeriodSelector() (+19 more)

### Community 19 - "Marked Days Tests"
Cohesion: 0.24
Nodes (10): MarkedDaysControllerTests, BadRequestObjectResult, Fact, IReadOnlyList, NoContentResult, NotFoundObjectResult, OkObjectResult, Task (+2 more)

### Community 20 - "MachineSnapshot"
Cohesion: 0.08
Nodes (22): MachineSnapshot, BeverageCounterCoffee, BeverageCounterCoffeeAndMilk, BeverageCounterHotWater, BeverageCounterHotWaterCups, BeverageCounterMilk, CreatedAt, Id (+14 more)

### Community 21 - "charts.test.tsx"
Cohesion: 0.11
Nodes (24): HeatmapDataPoint, birthday, range, COLORS, ConsumptionPieChart(), Props, DAYS, getColor() (+16 more)

### Community 22 - "MigrationBaseliner"
Cohesion: 0.09
Nodes (17): MigrationBaseliner, DateTime, MigrationBuilder, Initial, DateTime, ModelBuilder, DateTime, MigrationBuilder (+9 more)

### Community 23 - "TS App Config"
Cohesion: 0.09
Nodes (21): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection, moduleResolution (+13 more)

### Community 24 - "CoffeeTest.Helpers"
Cohesion: 0.17
Nodes (4): CoffeeTest.Helpers, CoffeeTest.Services, microsoft_extensions_logging, microsoft_extensions_options

### Community 25 - "package.json"
Cohesion: 0.13
Nodes (18): name, private, type, version, date-fns, eslint, @eslint/js, eslint-plugin-react-hooks (+10 more)

### Community 26 - "Frontend Dev Dependencies"
Cohesion: 0.10
Nodes (21): devDependencies, eslint, @eslint/js, eslint-plugin-react-hooks, eslint-plugin-react-refresh, globals, jsdom, tailwindcss (+13 more)

### Community 27 - "ApiIntegrationTests.cs"
Cohesion: 0.14
Nodes (15): CoffeeTest.Middleware, CoffeeTest.Integration, microsoft_aspnetcore_builder, microsoft_aspnetcore_hosting, microsoft_aspnetcore_http, microsoft_aspnetcore_mvc_testing, microsoft_aspnetcore_testhost, microsoft_extensions_configuration (+7 more)

### Community 28 - "react"
Cohesion: 0.21
Nodes (15): EventType, MarkDayEventModal(), MarkAsBackfillModal(), Props, existingEvent, massImportDay, useEscapeKey(), focusableElements() (+7 more)

### Community 29 - "TS Node Config"
Cohesion: 0.10
Nodes (19): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+11 more)

### Community 30 - "CoffeeTest.csproj"
Cohesion: 0.11
Nodes (16): net10.0, Microsoft.EntityFrameworkCore.Sqlite (10.0.12), net10.0, Microsoft.EntityFrameworkCore.Sqlite (10.0.12), coverlet.collector (6.0.2), Microsoft.AspNetCore.Mvc.Testing (10.0.12), Microsoft.AspNetCore.OpenApi (10.0.12), Microsoft.EntityFrameworkCore.Design (10.0.12) (+8 more)

### Community 31 - "Marked Days Controller"
Cohesion: 0.19
Nodes (10): MarkedDaysController, HttpDelete, HttpGet, HttpPost, IActionResult, ProducesResponseType, Task, IMarkedDayService (+2 more)

### Community 32 - "Marked Day Service"
Cohesion: 0.15
Nodes (13): MarkedDayError, AlreadyMarked, InvalidDate, InvalidEventType, InvalidKind, None, ReasonRequired, MarkedDayService (+5 more)

### Community 33 - "CoffeeStatusDto"
Cohesion: 0.07
Nodes (34): CoffeeStatusDto, Label, LastUpdated, Message, OperationState, PowerState, Reachable, Status (+26 more)

### Community 34 - "SnapshotResponseDto.cs"
Cohesion: 0.08
Nodes (28): DailyAggregateDto, BeanHoppers, CoffeeCount, Date, MilkCount, Total, DailyStatsResponseDto, Date (+20 more)

### Community 35 - "ApiKeyMiddleware"
Cohesion: 0.09
Nodes (29): ApiKeyMiddleware, ApiKeyMiddlewareExtensions, ProtectedRoute, IApplicationBuilder, IConfiguration, ILogger, IWebHostEnvironment, RequestDelegate (+21 more)

### Community 36 - "StatsController"
Cohesion: 0.31
Nodes (11): StatsController, Dictionary, HttpGet, IActionResult, ILogger, List, ProducesResponseType, Task (+3 more)

### Community 37 - "arc42/README.md"
Cohesion: 0.06
Nodes (37): 1.1 Purpose, 1.2 Requirements Overview, 1.3 Stakeholders, 1.4 Quality Goals, 1. Introduction and Goals, Capabilities, Non-functional requirements, 2.1 Technical Constraints (+29 more)

### Community 38 - ".BoundsUtc"
Cohesion: 0.27
Nodes (7): LocalDay, DateOnly, DateTime, LocalDayTests, Fact, 5.2.2 White box: the snapshot services, Architecture and code quality

### Community 39 - "SnapshotStatisticsService"
Cohesion: 0.18
Nodes (11): ISnapshotStatisticsService, DateOnly, List, Task, SnapshotStatisticsService, DateOnly, DateTime, HashSet (+3 more)

### Community 40 - "Snapshot Query Service"
Cohesion: 0.36
Nodes (5): SnapshotQueryService, DateOnly, DateTime, List, Task

### Community 41 - "CoffeeStatusController"
Cohesion: 0.10
Nodes (21): CoffeeStatusController, HttpGet, IActionResult, ProducesResponseType, Task, TimeSpan, IngestController, ILogger (+13 more)

### Community 42 - "Snapshot Query Interface"
Cohesion: 0.33
Nodes (5): ISnapshotQueryService, DateOnly, DateTime, List, Task

### Community 43 - "vite.config.ts"
Cohesion: 0.14
Nodes (14): dashboardRoot, viteConfigFile, buildTime, CONFIG_DIR, readCommitFromGitDir(), readText(), resolveGitDir(), ref_node_fs (+6 more)

### Community 44 - "API Specification: Coffee Analytics Hub"
Cohesion: 0.11
Nodes (17): API Specification: Coffee Analytics Hub, Base URL, Bohnenfach-Zuordnung, Datentypen, Fehlerbehandlung, Home Connect Keys (Relevant), HTTP Status Codes, Idempotenz-Regeln (+9 more)

### Community 45 - "HomeConnectService"
Cohesion: 0.08
Nodes (23): HomeConnectService, HttpClient, ILogger, LoggerMessage, Task, 3.1 Business Context, 3.2 Technical Context, 3.3.1 Ingest flow (n8n → API → SQLite) (+15 more)

### Community 46 - "Frontend Dependencies"
Cohesion: 0.22
Nodes (9): dependencies, date-fns, lucide-react, react, react-dom, react-router-dom, recharts, @sentry/react (+1 more)

### Community 47 - "Marked Day Entity"
Cohesion: 0.22
Nodes (8): MarkedDay, CreatedAt, Date, EventType, Kind, Reason, DateOnly, DateTime

### Community 48 - "NPM Scripts"
Cohesion: 0.25
Nodes (8): scripts, build, dev, lint, preview, test, test:coverage, test:watch

### Community 49 - "SnapshotResponseDto"
Cohesion: 0.11
Nodes (18): HealthResponseDto, Database, LastSnapshot, Status, Timestamp, SnapshotResponseDto, BeanHoppers, BeverageCounterCoffee (+10 more)

### Community 50 - "Marked Day DTO"
Cohesion: 0.25
Nodes (7): MarkedDayDto, CreatedAt, Date, EventType, Kind, Reason, DateTime

### Community 51 - "IngestPayloadValidatorTests"
Cohesion: 0.24
Nodes (7): IngestPayloadValidator, IReadOnlyList, IngestPayloadValidatorTests, Fact, InlineData, List, Theory

### Community 52 - "Coffee Analytics Hub"
Cohesion: 0.11
Nodes (18): API Endpoints, Architektur, Authentifizierung, CI/CD, Coffee Analytics Hub, Dashboard Features, Datensicherung, Docker Deployment (Produktion) (+10 more)

### Community 53 - "AddBeanHopperOverrides"
Cohesion: 0.14
Nodes (9): DateTime, MigrationBuilder, AddBeanHopperOverrides, DateTime, ModelBuilder, MigrationBuilder, AddEstimatedSnapshotFlag, DateTime (+1 more)

### Community 54 - "UnreachableSnapshotQueryService"
Cohesion: 0.22
Nodes (6): UnreachableSnapshotQueryService, LatestRequested, DateOnly, DateTime, List, Task

### Community 55 - "SentryErrorBoundary.test.tsx"
Cohesion: 0.16
Nodes (6): flakyChild, MockErrorBoundary, coffee_dashboard_src_index, initSentry(), react-dom, @sentry/react

### Community 56 - "8. Cross-cutting Concepts"
Cohesion: 0.12
Nodes (16): 8.1.1 MachineSnapshot, 8.1.2 MarkedDay, 8.1 Domain Model, 8.2 Idempotency, 8.3 Time and Timezone Handling, 8.4 Security, 8.7 Testing, 8.8 Observability (+8 more)

### Community 57 - ".Main"
Cohesion: 0.19
Nodes (8): ISnapshotIngestService, Task, SnapshotIngestService, ILogger, LoggerMessage, Task, ADR-012: Snapshot Services Split by Responsibility, ILoggerFactory

### Community 58 - "Create Marked Day DTO"
Cohesion: 0.40
Nodes (5): CreateMarkedDayDto, Date, EventType, Kind, Reason

### Community 59 - "DbContext Model Snapshot"
Cohesion: 0.40
Nodes (4): AppDbContextModelSnapshot, DateTime, ModelBuilder, ModelSnapshot

### Community 60 - "Build & Push Script"
Cohesion: 0.67
Nodes (3): build_and_push(), SERVICES, build.sh script

### Community 64 - "IngestControllerTests"
Cohesion: 0.41
Nodes (6): IngestControllerTests, BadRequestObjectResult, Fact, OkObjectResult, Task, CreatedResult

### Community 65 - "CollectingLogger"
Cohesion: 0.21
Nodes (9): CollectingLogger, CollectingLoggerProvider, EventId, Exception, Func, IDisposable, ILogger, List (+1 more)

### Community 66 - "Endpoints"
Cohesion: 0.13
Nodes (15): 10. DELETE /api/stats/marked-days/{date}, 11. GET /coffee/status, 12. POST /coffee/power, 3. POST /api/admin/historical-backfill/preview, 4. POST /api/admin/historical-backfill/apply, 6. GET /api/stats/range, 8. GET /api/stats/marked-days, Endpoints (+7 more)

### Community 67 - "pages.test.tsx"
Cohesion: 0.19
Nodes (10): fetchHeatmap(), useHeatmap(), HeatmapPage(), weekOptions, daily, estimatedPage, heatmap, massImport (+2 more)

### Community 68 - ".Ingest"
Cohesion: 0.49
Nodes (3): SnapshotIngestServiceTests, Fact, Task

### Community 69 - "4. Solution Strategy"
Cohesion: 0.15
Nodes (12): ModelBuilder, 4.1 Technology Decisions, 4.2 Decomposition Strategy, 4.3 Handling the Core Domain Problem: Counters, not Events, 4.4 Time Strategy, 4.5 Safety and Degradation Strategy, 4.6 Migration Strategy, 4.7 Quality Assurance Strategy (+4 more)

### Community 70 - "backup.sh"
Cohesion: 0.42
Nodes (11): apply_retention_policy(), create_online_sqlite_backup(), flush_filesystem_buffers(), load_config(), log(), main(), require_source_database(), backup.sh script (+3 more)

### Community 71 - "test-backup.sh"
Cohesion: 0.33
Nodes (11): assert_eq(), restore_system_backup_env_if_needed(), set_file_mtime_days_ago(), test-backup.sh script, test_env_file_sourcing(), test_invalid_retention_fails(), test_invalid_timeout_fails(), test_missing_source_fails() (+3 more)

### Community 72 - "AppDbContext"
Cohesion: 0.20
Nodes (8): AppDbContext, BeanHopperOverrides, MachineSnapshots, MarkedDays, DesignTimeDbContextFactory, DbContext, DbSet, IDesignTimeDbContextFactory

### Community 73 - ".Ingest"
Cohesion: 0.20
Nodes (10): HttpPost, IActionResult, ProducesResponseType, Task, IngestResponseDto, Created, Id, Message (+2 more)

### Community 74 - "IngestDataDto"
Cohesion: 0.22
Nodes (10): IngestDataDto, Status, IngestPayloadDto, Data, StatusItemDto, Key, Unit, Value (+2 more)

### Community 75 - "9. Design Decisions"
Cohesion: 0.18
Nodes (11): 9. Design Decisions, ADR-001: SQLite over MongoDB, ADR-002: n8n as Cloud Gateway, ADR-003: Scalar over Swagger/Swashbuckle, ADR-004: Client-Driven Timezone Offset, ADR-005: Counter-Based Idempotency, ADR-006: React over Blazor, ADR-007: Migration Baseliner (+3 more)

### Community 76 - "Review guidelines"
Cohesion: 0.20
Nodes (8): graphify, P0 — block the merge, P1 — should fix, Project context, Review guidelines, Tech stack, Testing expectations, What NOT to flag

### Community 77 - "EstimatedSnapshotFlagMigrationTests"
Cohesion: 0.31
Nodes (5): EstimatedSnapshotFlagMigrationTests, Fact, Task, IDisposable, IMigrator

### Community 78 - "Program.cs"
Cohesion: 0.22
Nodes (6): CoffeeApi, microsoft_aspnetcore_httpoverrides, microsoft_aspnetcore_ratelimiting, scalar_aspnetcore, system_data, system_globalization

### Community 79 - "PaginatedResponseDto"
Cohesion: 0.25
Nodes (8): PaginatedResponseDto, Data, Pagination, PaginationDto, Page, PageSize, TotalItems, TotalPages

### Community 81 - "CLAUDE.md"
Cohesion: 0.29
Nodes (5): Common commands, Conventions, graphify, Project, Stack

### Community 82 - "DropUnusedIdempotencyIndex"
Cohesion: 0.33
Nodes (4): MigrationBuilder, DropUnusedIdempotencyIndex, DateTime, ModelBuilder

### Community 83 - "5.2 Level 2 — CoffeeApi"
Cohesion: 0.29
Nodes (7): 5.1 Level 1 — System Decomposition, 5.2.1 Layer dependencies, 5.2.4 Persistence model, 5.2 Level 2 — CoffeeApi, 5.3.1 Frontend data flow, 5.3 Level 2 — coffee-dashboard, 5. Building Block View

### Community 84 - "Correctness"
Cohesion: 0.33
Nodes (6): 10.2 Quality Scenarios, Correctness, Maintainability, Performance, Security, Usability

### Community 85 - "entrypoint.sh"
Cohesion: 0.60
Nodes (3): install_backup_cron_job(), entrypoint.sh script, write_backup_env_file()

### Community 86 - "Coffee Dashboard"
Cohesion: 0.40
Nodes (4): Coffee Dashboard, Docker, Entwicklung, Tech Stack

### Community 87 - "InvalidIngestPayloadException"
Cohesion: 0.40
Nodes (4): InvalidIngestPayloadException, Details, IReadOnlyList, Exception

### Community 88 - "11. POST /api/stats/snapshots/{id}/bean-hopper"
Cohesion: 0.40
Nodes (5): 11. POST /api/stats/snapshots/{id}/bean-hopper, Body, Path Parameters, Request, Responses

### Community 89 - "1. POST /api/ingest"
Cohesion: 0.40
Nodes (5): 1. POST /api/ingest, Request, Response (200 OK - Duplikat/Keine Änderung), Response (201 Created - Neuer Snapshot), Response (400 Bad Request)

### Community 90 - "5. GET /api/stats/daily/{date}"
Cohesion: 0.40
Nodes (5): 5. GET /api/stats/daily/{date}, Path Parameters, Query Parameters, Request, Response (200 OK)

### Community 91 - "10. DELETE /api/stats/snapshots/{id}/bean-hopper"
Cohesion: 0.50
Nodes (4): 10. DELETE /api/stats/snapshots/{id}/bean-hopper, Query Parameters, Request, Responses

### Community 92 - "2. GET /api/stats"
Cohesion: 0.50
Nodes (4): 2. GET /api/stats, Query Parameters, Request, Response (200 OK)

### Community 93 - "7. GET /api/stats/heatmap"
Cohesion: 0.50
Nodes (4): 7. GET /api/stats/heatmap, Query Parameters, Request, Response (200 OK)

### Community 94 - "n8n Workflow Spezifikation"
Cohesion: 0.50
Nodes (4): Cron Expression, Erwartete Frequenz, n8n Workflow Spezifikation, Workflow-Ablauf

### Community 96 - "13. GET /api/health"
Cohesion: 0.67
Nodes (3): 13. GET /api/health, Response (200 OK) — Datenbank erreichbar, Response (200 OK) — Datenbank nach Start nicht erreichbar

### Community 97 - "9. POST /api/stats/marked-days"
Cohesion: 0.67
Nodes (3): 9. POST /api/stats/marked-days, Request, Responses

## Knowledge Gaps
- **464 isolated node(s):** `net10.0`, `Microsoft.AspNetCore.OpenApi (10.0.12)`, `Scalar.AspNetCore (2.0.36)`, `Microsoft.EntityFrameworkCore.Sqlite (10.0.12)`, `Microsoft.EntityFrameworkCore.Design (10.0.12)` (+459 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 687 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AppDbContext` connect `AppDbContext` to `.Create`, `BeanHopperService`, `StatsControllerTests`, `HistoricalBackfillService`, `Recording Test Logger`, `Factory`, `.EnsureBaselined`, `ApiIntegrationTests`, `CoffeeApi.Infrastructure`, `Marked Days Tests`, `MachineSnapshot`, `Marked Day Service`, `StatsController`, `.BoundsUtc`, `SnapshotStatisticsService`, `Snapshot Query Service`, `Marked Day Entity`, `.Main`, `.Ingest`, `4. Solution Strategy`, `Review guidelines`, `EstimatedSnapshotFlagMigrationTests`?**
  _High betweenness centrality (0.119) - this node is a cross-community bridge._
- **Why does `Functional requirements` connect `CoffeeStatusController` to `arc42/README.md`, `SnapshotStatisticsService`, `HomeConnectService`, `DashboardPage.tsx`, `.Main`?**
  _High betweenness centrality (0.080) - this node is a cross-community bridge._
- **Why does `Correctness` connect `Correctness` to `.Create`, `StatsControllerTests`, `DailyBarChart.tsx`, `DashboardPage.tsx`, `Marked Days Tests`, `MachineSnapshot`?**
  _High betweenness centrality (0.079) - this node is a cross-community bridge._
- **Are the 76 inferred relationships involving `SnapshotBuilder` (e.g. with `.CreateAsync()` and `.GetAll_MapsEstimatedFlag()`) actually correct?**
  _`SnapshotBuilder` has 76 INFERRED edges - model-reasoned connections that need verification._
- **Are the 11 inferred relationships involving `MachineSnapshot` (e.g. with `.DefaultValues_AreCorrect()` and `.TotalBeverages_SumsAllCountersExceptHotWaterMl()`) actually correct?**
  _`MachineSnapshot` has 11 INFERRED edges - model-reasoned connections that need verification._
- **Are the 5 inferred relationships involving `AppDbContext` (e.g. with `P1 — should fix` and `4.2 Decomposition Strategy`) actually correct?**
  _`AppDbContext` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net10.0`, `Microsoft.AspNetCore.OpenApi (10.0.12)`, `Scalar.AspNetCore (2.0.36)` to the rest of the system?**
  _464 weakly-connected nodes found - possible documentation gaps or missing edges._