# 2. Architecture Constraints

Constraints are conditions the architecture must accept. They are not
decisions — decisions are in [04](04-solution.md) and [09](09-design.md).

## 2.1 Technical Constraints

| ID | Constraint | Consequence for the architecture |
|----|-----------|----------------------------------|
| TC-1 | **The EQ900 exposes only lifetime counters, no events** | Consumption must be derived as a delta between samples. Any gap in sampling is a gap in resolution, not in totals. |
| TC-2 | **Home Connect access requires OAuth2 and is rate-limited** | Token handling is delegated to n8n ([ADR-002](09-design.md#adr-002-n8n-as-cloud-gateway)); the API never speaks to BSH directly. The status endpoint caches for 7 s. |
| TC-3 | **LAN-only deployment** | Nothing is published to the internet. No TLS termination, no reverse-proxy auth, no public DNS. Only n8n has outbound internet access. This is the load-bearing security control. |
| TC-4 | **SQLite as the datastore** | One writer at a time. No server process, no connection pool tuning, no network round trip. Backup is a file copy. Rules out horizontal scaling — accepted. |
| TC-5 | **.NET 10** | Backend targets `net10.0` (`CoffeeApi/CoffeeApi.csproj`). Nullable reference types are enabled and the build must stay warning-free. |
| TC-6 | **React 19 / Vite / TypeScript** | Frontend is a static bundle served by nginx. No SSR, no Node runtime in production. |
| TC-7 | **Docker + Portainer on a Synology NAS** | Both components ship as OCI images to GHCR. Deployment is a manual pull; no orchestrator, no rolling update, no readiness gate beyond `/api/health`. |
| TC-8 | **n8n owns the schedule** | The API has no background service, no timer, no hosted service. If n8n stops, ingest stops silently — the API cannot tell the difference between "no coffee was made" and "no data arrived". |
| TC-9 | **Single machine instance** | `MachineId` is present on the entity but always `"EQ900-DEFAULT"`. Some queries do not filter on it at all (see [11](11-risks.md)). |

## 2.2 Organisational Constraints

| ID | Constraint | Consequence |
|----|-----------|-------------|
| OC-1 | **Solo developer** | Complexity has a direct maintenance cost with no team to absorb it. Simplicity outranks flexibility (`CLAUDE.md`, global guardrails §2). |
| OC-2 | **Self-hosted only** | No cloud SaaS for core services. Error tracking is a self-hosted GlitchTip, not Sentry SaaS. Code review tooling and CI run on GitHub Actions — the one accepted exception. |
| OC-3 | **Tests ship with behaviour** | Repository convention (`CLAUDE.md`): business logic in `Services/` and every controller branch is covered, in the same change as the behaviour. |
| OC-4 | **AI-assisted contributions** | `CLAUDE.md` and `AGENTS.md` define conventions that automated contributors are expected to follow; PRs are reviewed by an automated reviewer with a severity gate (`.github/workflows/review.yml`). |
| OC-5 | **Merges are performed by the repository owner** | No agent or automation merges to `main`. |

## 2.3 Conventions

| Convention | Where it is defined | Enforcement |
|------------|--------------------|-------------|
| Controller → Service → EF Core | `CLAUDE.md` | Review; not enforced by tooling (and currently violated in `StatsController`, see [11](11-risks.md)) |
| DTOs mapped at the controller boundary; entities never leave the API | `CLAUDE.md` | Review |
| `Nullable` enabled, warning-free | `CoffeeApi.csproj` | Compiler |
| Async all the way down | `CLAUDE.md` | Review |
| No explanatory comments — extract a named method instead | `AGENTS.md` | Review |
| Magic numbers become named constants | `AGENTS.md` | Review |
| German user-facing labels | Implicit in the dashboard | Review |
| Conventional commit types (`feat`, `fix`, `chore`, `ci`, `docs`) | Git history | Convention |

## 2.4 Contractual and External Constraints

| Constraint | Detail |
|------------|--------|
| **BSH Home Connect terms** | Rate limits and acceptable-use policy apply. Polling cadence (15 min) and the 7-second status cache exist to stay inside them. |
| **No personal data** | The system stores machine counters and timestamps only. No user accounts, no names, no identifiers. Sentry runs with `SendDefaultPii = false` on both sides. This keeps GDPR obligations out of scope — it is a constraint, not a feature. |
| **MIT License** | `LICENSE` at repository root. |
