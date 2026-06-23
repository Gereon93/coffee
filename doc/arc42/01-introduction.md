# 1. Introduction and Goals

## 1.1 Purpose

The Coffee Analytics Hub tracks and visualises the coffee consumption of a
Siemens EQ900 espresso machine. It provides:

- **Automated data capture**: Counter readings are pulled from the BSH Home
  Connect API every 15 minutes via an n8n workflow and stored in a SQLite
  database.
- **Analytics dashboard**: A React SPA visualises daily consumption, trends,
  heatmaps, peak hours, weekday comparisons, and anomaly detection.
- **Machine control**: Power on/off and live status via a relay through n8n
  to the Home Connect API.
- **Manual annotations**: Users can mark days as mass-import (excluded from
  statistics) or annotate them with events (birthday, visitors, etc.).

## 1.2 Stakeholders

| Stakeholder | Role | Interest |
|-------------|------|----------|
| Developer / Operator | Maintains and deploys the system | Clean architecture, maintainability, observability |
| End User (household) | Uses the dashboard daily | Intuitive UI, reliable data, quick insights |
| n8n Workflow | External data fetcher | Stable API contract, idempotent ingest |

## 1.3 Quality Goals

| Priority | Goal | Scenario |
|----------|------|----------|
| High | Data correctness | Counter deltas are computed correctly, including cross-day boundaries and timezone handling |
| High | Idempotent ingest | Duplicate payloads from n8n do not create duplicate records |
| High | Reliability | System operates unattended 24/7; n8n handles retries and token refresh |
| Medium | Performance | Read API responds in < 100ms for typical queries |
| Medium | Simplicity | Single SQLite file for storage; easy backup by file copy |
| Low | Scalability | Designed for single-machine use; not multi-tenant |
