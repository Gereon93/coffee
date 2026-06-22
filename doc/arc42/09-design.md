# 9. Design Decisions

## ADR-001: SQLite over MongoDB

- **Context:** The original Nivona-based system used MongoDB. After pivoting to the EQ900, the storage layer was reconsidered.
- **Decision:** Use SQLite with EF Core.
- **Rationale:** Single-file storage, zero-config, easy backup (just copy the file), no server process needed. Sufficient for single-machine scope with ~76 writes/day.
- **Consequences:** No concurrent writers; no horizontal scaling. Acceptable for the use case.

## ADR-002: n8n as Cloud Gateway

- **Context:** The BSH Home Connect API requires OAuth2 authentication and has rate limits.
- **Decision:** Delegate all cloud communication to n8n; the Coffee API only communicates with n8n via LAN.
- **Rationale:** OAuth2 token management, scheduling, retries, and error notifications are n8n's strengths. Keeps the API simple and avoids exposing internet-facing ports on the NAS.
- **Consequences:** Dependency on n8n availability; additional debugging layer.

## ADR-003: Scalar over Swagger/Swashbuckle

- **Context:** API documentation UI is needed for development and debugging.
- **Decision:** Use Scalar (`Scalar.AspNetCore`) instead of Swashbuckle.
- **Rationale:** Modern UI, better UX, native OpenAPI 3.0 support via `Microsoft.AspNetCore.OpenApi`.
- **Consequences:** Smaller community than Swagger; but sufficient for the project's needs.

## ADR-004: Client-Driven Timezone

- **Context:** The system runs in Europe/Berlin but timestamps are stored in UTC.
- **Decision:** The frontend sends its UTC offset as a `tz` query parameter; the backend uses it for day boundary computation.
- **Rationale:** Avoids hardcoding timezone on the server; supports any client timezone. No JavaScript `Intl` needed on the backend.
- **Consequences:** Every time-aware endpoint needs the `tz` parameter. The frontend handles this transparently.

## ADR-005: Counter-Based Idempotency

- **Context:** n8n delivers the same payload every 15 minutes if counters haven't changed.
- **Decision:** Only persist a new snapshot when at least one beverage counter has increased.
- **Rationale:** Prevents duplicate records; reduces storage; simplifies delta computation.
- **Consequences:** Status-only changes (e.g. OperationState) are not recorded. Acceptable because consumption is the primary interest. Counter resets (e.g. after machine maintenance) are also not recorded — the reset baseline is lost, causing incorrect deltas until counters exceed the previous high. This is a known limitation (see TD-15 in risks).

## ADR-006: React over Blazor

- **Context:** The original frontend was Blazor-based (CoffeeWeb).
- **Decision:** Migrate to React + Vite + TypeScript.
- **Rationale:** Larger ecosystem, better charting libraries (Recharts), faster HMR, more community support.
- **Consequences:** Two separate runtimes (.NET + Node) in the project.

## ADR-007: Migration Baseliner

- **Context:** The production SQLite database was created with `EnsureCreated()` before EF Core migrations were introduced.
- **Decision:** Implement a `MigrationBaseliner` that detects pre-migration databases and seeds `__EFMigrationsHistory` with the initial migration ID.
- **Rationale:** Allows `Database.Migrate()` to run safely on existing databases without re-creating tables or losing data.
- **Consequences:** One-time migration path; new databases skip the baseliner automatically.

## ADR-008: MarkedDay Dual-Kind Design

- **Context:** Days may need to be excluded from statistics (mass-import) or annotated with events (birthday, visitors).
- **Decision:** Single `MarkedDay` entity with a `Kind` discriminator (`mass-import` or `event`). One MarkedDay per date.
- **Rationale:** Simpler than two separate tables; the two kinds have similar structure. The `Kind` determines validation rules and semantics.
- **Consequences:** Cannot have both kinds on the same date. Acceptable because mass-import and event are mutually exclusive in practice.
