# 4. Solution Strategy

## 4.1 Architectural Patterns

| Decision | Rationale |
|----------|-----------|
| **Controller → Service → EF Core** | Clear separation of concerns; business logic in services, not controllers |
| **DTOs at the controller boundary** | Entities are never exposed over the API; DTOs are mapped in the controller |
| **Idempotent ingest** | Counter-based deduplication prevents duplicate records when n8n retries |
| **n8n as cloud gateway** | Keeps OAuth2 complexity out of the API; no internet-facing ports needed |
| **SQLite for storage** | Single-file, zero-config, easy backup; sufficient for single-machine scope |
| **Scalar for API docs** | Modern Swagger replacement with better UX |
| **React + Vite + TanStack Query** | Fast DX, client-side caching, type-safe API contracts |
| **Tailwind CSS v4** | Utility-first styling; dark mode via system preference |

## 4.2 Timezone Strategy

All timestamps are stored as UTC in SQLite. The frontend sends its UTC offset
(`tz` query parameter, in minutes) so the backend can compute local day
boundaries for aggregation. This avoids hardcoding CET/CEST and supports
arbitrary client timezones.

## 4.3 Cross-Day Delta Strategy

Daily and range statistics compute consumption as the delta between the last
and first snapshot of a day. To handle the first delta correctly, the previous
day's last snapshot is fetched as a baseline. This ensures that beverages made
early in the morning (before the first snapshot) are counted.

## 4.4 Error Tracking

Both backend and frontend integrate with a self-hosted GlitchTip instance
(Sentry-API-compatible). An empty DSN disables error tracking entirely,
allowing local development without network calls.

## 4.5 Migration Strategy

EF Core migrations run automatically at container startup. A
`MigrationBaseliner` detects pre-migration databases (created with
`EnsureCreated()`) and seeds the `__EFMigrationsHistory` table so that
`Database.Migrate()` does not attempt to re-create existing tables.
