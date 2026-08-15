# 12. Glossary

## Domain terms

| Term | Definition |
|------|-----------|
| **Anomaly** | A day whose total consumption exceeds the mean of the selected range by more than 1.5 standard deviations. Computed client-side over the range currently displayed, after **all** annotated days — `mass-import` *and* `event` — have been removed from the baseline. |
| **Baseline** | The snapshot used as the subtrahend when computing a period's consumption. Normally the last snapshot strictly *before* the period; only when none exists does the period's own first snapshot serve. |
| **Beverage counter** | A monotonically increasing lifetime count for one beverage category, as reported by the machine. Never reset by the application. |
| **Counter** | Short for beverage counter. |
| **Cross-day delta** | A consumption delta computed across midnight, using the previous day's last snapshot as the baseline. Exists so that beverages brewed before the day's first sample are attributed to the correct day. |
| **Delta** | The difference between two counter readings — the actual consumption between them. |
| **Event** | A `MarkedDay` kind: real data with a known explanation (`birthday`, `visitors`, `party`, `sick`, `vacation`, `other`). Stays in consumption totals and the heatmap, but is excluded from anomaly detection. |
| **Heatmap** | Consumption aggregated into a weekday (1 = Monday … 7 = Sunday) × hour matrix over a rolling window of *n* weeks. |
| **Idempotency** | Here: processing the same ingest payload any number of times produces exactly one stored row and the same response. |
| **Ingest** | Receiving a Home Connect payload from n8n and, if the counters increased, persisting it as a snapshot. |
| **Mass-import** | A `MarkedDay` kind for bulk-backfilled data whose timestamps do not reflect when coffee was actually brewed. Excluded from the heatmap and from anomaly detection. |
| **MarkedDay** | A manual annotation on a single local date. Exactly one per date; the `Kind` field decides its semantics. |
| **Peak hour** | The local hour containing the largest single positive jump in `TotalBeverages` on a given day. |
| **Snapshot** | One reading of the machine's counters and status at a point in time. The atom of this system's data model. |
| **Time period** | The dashboard's range selector: `week`, `month`, `year`, or `all`. Weeks start on Monday. |
| **Total beverages** | `Coffee + CoffeeAndMilk + Milk + HotWaterCups`. Deliberately excludes hot water measured in millilitres. |

## External systems and products

| Term | Definition |
|------|-----------|
| **BSH** | BSH Hausgeräte GmbH — manufacturer of Bosch, Siemens, Neff and Gaggenau appliances. |
| **EQ900** | The Siemens fully-automatic espresso machine this system observes. |
| **GHCR** | GitHub Container Registry — hosts the two published Docker images. |
| **GlitchTip** | Self-hosted, Sentry-API-compatible error tracking. Receives events from both the API and the dashboard. |
| **Home Connect** | BSH's cloud platform and REST API for appliance integration. Requires OAuth2 and is rate-limited. |
| **n8n** | Self-hosted workflow automation platform. Owns the schedule, the OAuth2 credentials, and both directions of BSH communication. |
| **Portainer** | Web UI for Docker management, running on the Synology NAS. |
| **Scalar** | The OpenAPI documentation UI used instead of Swagger. Served at `/scalar/v1`. |
| **Sentry** | The SDK and wire protocol used for error reporting; the receiving server here is GlitchTip. |
| **SonarQube** | Static analysis, run against `main` when the corresponding secrets are configured. |

## Technical terms

| Term | Definition |
|------|-----------|
| **ADR** | Architecture Decision Record — a decision with its context, alternatives, and consequences. See [09](09-design.md). |
| **arc42** | The template this documentation follows. |
| **DTO** | Data Transfer Object — the shapes crossing the HTTP boundary. Entities never do. |
| **EF Core** | Entity Framework Core — the ORM, used code-first with migrations. |
| **Half-open interval** | `[start, end)` — start included, end excluded. Used for all day and range boundaries so that adjacent days neither overlap nor leave a gap. |
| **Migration baselining** | Seeding `__EFMigrationsHistory` for a database created before migrations existed, so that `Migrate()` applies only genuinely pending migrations. |
| **TanStack Query** | The frontend server-state library — caching, retry, and invalidation. |
| **`tz`** | The query parameter carrying the caller's UTC offset **in minutes** (60 = CET, 120 = CEST). Not an IANA zone id — see [ADR-004](09-design.md#adr-004-client-driven-timezone-offset). |
| **Value converter** | EF Core mechanism used here for two things: `DateOnly ↔ "yyyy-MM-dd"`, and re-stamping every `DateTime` read from SQLite as `DateTimeKind.Utc`. |

## Conventions used in this documentation

| Marker | Meaning |
|--------|---------|
| **Known deviation** | Documented behaviour that differs from what a reader would reasonably expect. Always cross-referenced to [11](11-risks.md). |
| **Gap** | Something absent that the project's own conventions call for. |
| `TD-nn` | An entry in the technical debt list, [11.1](11-risks.md#111-technical-debt). |
| `Q-nn` | A quality scenario, [10.2](10-quality.md#102-quality-scenarios). |
| `ADR-nnn` | A design decision, [09](09-design.md). |
| `TC-` / `OC-` / `FR-` | Technical constraint, organisational constraint, functional requirement. |
