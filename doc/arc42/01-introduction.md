# 1. Introduction and Goals

## 1.1 Purpose

The Coffee Analytics Hub turns the lifetime beverage counters of a Siemens
EQ900 espresso machine into a consumption history and a set of visualisations.

The machine itself exposes only *monotonically increasing lifetime counters* —
"12 847 coffees ever". It has no notion of "today", no event stream, and no
history. The entire value of this system comes from one idea: **sample the
counters regularly, store the samples, and derive consumption as the delta
between samples.**

Everything else follows from that: the 15-minute polling cadence, the
idempotency rule, the cross-day baseline handling, and the fact that a counter
reset is a hard problem (see [ADR-005](09-design.md#adr-005-counter-based-idempotency)).

### Capabilities

| Capability | Description |
|------------|-------------|
| **Automated capture** | An n8n workflow polls the BSH Home Connect API every 15 minutes and POSTs the raw payload to `POST /api/ingest`. The API deduplicates and persists. |
| **Analytics dashboard** | A React SPA renders daily totals, trends, a consumption split, hourly peaks, a weekday×hour heatmap, and weekday comparison. |
| **Anomaly detection** | Days whose total exceeds the range mean by more than 1.5 standard deviations are flagged in the bar chart. |
| **Machine control** | Power on/off and live status, relayed through the API to n8n to Home Connect. |
| **Manual annotations** | A day can be marked `mass-import` (backfilled data, excluded from the heatmap and from anomaly detection) or `event` (real data with an explanation: birthday, visitors, party, sick, vacation, other — kept in consumption totals and the heatmap, but not flagged as an anomaly). |
| **Operational visibility** | `GET /api/health`, structured logs, Sentry/GlitchTip error tracking in both backend and frontend. |

## 1.2 Requirements Overview

### Functional requirements

| ID | Requirement | Realised by |
|----|-------------|-------------|
| FR-1 | Accept counter snapshots from n8n over HTTP | `IngestController` |
| FR-2 | Never store two consecutive snapshots with unchanged counters | `SnapshotService.HasCounterIncreased` |
| FR-3 | Report consumption per calendar day in the *user's* local timezone | `tz` query parameter, `GetLocalDayBoundsUtc` |
| FR-4 | Count beverages brewed before the first sample of a day against that day | Cross-day baseline (previous day's last snapshot) |
| FR-5 | Aggregate consumption into a weekday × hour heatmap | `SnapshotService.GetHeatmapDataAsync` |
| FR-6 | Exclude backfilled days from heatmap and anomaly detection | `MarkedDay` with `Kind = "mass-import"` |
| FR-7 | Annotate days with an explanation without excluding them | `MarkedDay` with `Kind = "event"` |
| FR-8 | Switch the machine on/off from the dashboard | `PowerController` → `HomeConnectService` → n8n |
| FR-9 | Show live machine status without exhausting the BSH quota | `CoffeeStatusController` with a 7-second in-memory cache |
| FR-10 | Browse raw snapshots with per-row counter deltas | `LogPage` + `GET /api/stats` |

### Non-functional requirements

Detailed and made measurable in [10 — Quality Requirements](10-quality.md).

## 1.3 Stakeholders

| Stakeholder | Role | Expectations |
|-------------|------|--------------|
| **Developer / Operator** (solo) | Designs, builds, deploys, and operates the whole stack | Clean layering, tests that catch regressions, deployment that is one `docker compose pull` away, errors visible in GlitchTip without SSH |
| **End user (household)** | Opens the dashboard, presses the power button | Correct numbers, German labels, works on a phone, no login friction inside the LAN |
| **n8n workflow** | Automated client of the ingest and power interfaces | A stable API contract, idempotent ingest so retries are harmless, non-ambiguous status codes (201 = stored, 200 = skipped) |
| **BSH Home Connect** | Upstream data source, quota owner | Polite polling; no request storm. Enforced by the 15-minute cadence and the 7-second status cache |
| **AI coding assistants** | Contribute code under `CLAUDE.md` / `AGENTS.md` | Documented conventions, an architecture description that matches the code |

## 1.4 Quality Goals

Top three, in priority order. These are the goals that decide trade-offs when
they conflict.

| # | Goal | Motivation | Concrete scenario |
|---|------|-----------|-------------------|
| 1 | **Data correctness** | The system exists to produce numbers. A wrong number is worse than a missing one — it is silently wrong and nobody notices. | A coffee brewed at 06:40 CEST, before the first sample of the day, is counted against that day and not the previous one. |
| 2 | **Idempotent, unattended ingest** | Nobody supervises the pipeline. n8n retries on its own. | The same payload delivered three times produces exactly one row. |
| 3 | **Operational simplicity** | One person maintains this next to a day job. | Backup is `cp coffee.db coffee.db.bak`. Deployment is pulling two images. |

Further goals, deliberately ranked lower:

| Goal | Rank | Note |
|------|------|------|
| Performance | Medium | Dataset is small (~76 writes/day). Read endpoints target < 100 ms. |
| Security | Medium | LAN-only deployment is the primary control; see [08.4](08-concepts.md#84-security) for what that does *not* cover. |
| Scalability | Low | Single machine, single household. `MachineId` exists as a seam but is never varied. |
| Portability | Low | Targets one Synology NAS running Portainer. |
