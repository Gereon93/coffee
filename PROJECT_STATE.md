# Project State: Coffee Analytics Hub

## Aktueller Fokus

**Phase:** Production - API + Dashboard live
**Status:** MVP komplett, beide Container laufen

---

## Meilensteine

| Phase | Status | Beschreibung |
|-------|--------|--------------|
| 1. Vision & Strategie | Done | Pivot zu EQ900/SQLite |
| 2. Architecture & Spec | Done | ARCHITECTURE.md, DESIGN.md, SPEC.md |
| 3. Infrastructure | Done | .NET 10, SQLite, Scalar |
| 4. Core API | Done | Ingest + Stats Endpoints |
| 5. n8n Integration | Done | Workflow alle 15 Min |
| 6. Read API | Done | Stats-Endpoints + Cross-Day Delta Fix |
| 7. Deployment | Done | Docker Registry, Portainer |
| 8. MongoDB Cleanup | Done | Legacy-Code entfernt |
| 9. React Frontend | Done | Dashboard + Heatmap live |
| 10. CoffeeWeb Cleanup | Done | Blazor-Projekt entfernt |
| 11. Build Script | Done | build.sh ersetzt CI Docker Stage |
| 12. CI/CD Pipeline | Teilweise | Build+Test in GitLab, Docker lokal via build.sh |
| 13. Testing | Offen | Alte Nivona-Tests obsolet, neue EQ900-Tests fehlen |

---

## Was laeuft produktiv

| Komponente | Status | Details |
|------------|--------|---------|
| Coffee API | Live | `192.168.2.143:8089` |
| Coffee Dashboard | Live | `192.168.2.143:8090` |
| SQLite DB | Live | Persistiert in Docker Volume |
| n8n Workflow | Live | Cron alle 15 Min (07:00-02:00) |
| Idempotenz | Funktioniert | Duplikate werden uebersprungen |
| Scalar UI | Live | `192.168.2.143:8089/scalar/v1` |
| API-Key Auth | Live | `/api/ingest` geschuetzt |
| Cross-Day Deltas | Funktioniert | Tagesuebergreifende Bezuege korrekt |

---

## Dashboard Features

| Feature | Status |
|---------|--------|
| KPI Cards (Heute + Zeitraum) | Done |
| Period Selector (Woche/Monat/Jahr/Gesamt) | Done |
| Gesamt: Absolute Counter vom letzten Snapshot | Done |
| Taeglicher Verbrauch (Stacked Bar) | Done |
| Verbrauchs-Trend (Area Chart) | Done |
| Verteilung Kaffee/Milch (Pie Chart) | Done |
| Heutige Peaks (Stundlich) | Done |
| Wochentag-Vergleich | Done |
| Heatmap (Wochentag x Stunde) | Done |
| Anomalie-Erkennung (Z-Score) | Done |
| Dark Mode (System Preference) | Done |

---

## Naechste Schritte (Priorisiert)

1. **Unit Tests (EQ900)**
   - SnapshotService: Idempotenz, Cross-Day Delta
   - StatsController: Range Aggregation, Daily Summary
   - Alte Nivona/MongoDB Tests ersetzen

2. **CI/CD vervollstaendigen**
   - GitLab Runner braucht .NET 10 SDK Image
   - Dann laufen Build + Test automatisch

3. **Dashboard Verbesserungen (optional)**
   - Date Picker fuer historische Tage
   - Dark Mode Toggle
   - Loading Skeletons statt Spinner

---

## Tech Stack (Final)

| Layer | Technologie |
|-------|-------------|
| Backend | ASP.NET Core (.NET 10) |
| Datenbank | SQLite + EF Core |
| API-Doku | Scalar |
| Frontend | React 19 + Vite + TypeScript |
| Charts | Recharts |
| State | TanStack React Query |
| Styling | Tailwind CSS v4 |
| Scheduler | n8n (extern) |
| Auth | API-Key Header |
| Container | Docker (nginx + .NET) |
| Registry | GitLab (192.168.2.143:5050) |
| Hosting | Portainer auf NAS |
| Build | build.sh (lokal) |

---

## API Endpoints

| Method | Endpoint | Beschreibung |
|--------|----------|--------------|
| POST | `/api/ingest` | n8n liefert Snapshots (API-Key required) |
| GET | `/api/stats` | Alle Snapshots (paginiert) |
| GET | `/api/stats/daily/{date}` | Tagesstatistik + Snapshots |
| GET | `/api/stats/range?from=&to=` | Zeitraum-Aggregation |
| GET | `/api/stats/heatmap?weeks=4` | Heatmap-Daten |
| GET | `/api/health` | Health Check |
| GET | `/scalar/v1` | API Dokumentation |

---

## Docker Images

| Image | Registry |
|-------|----------|
| `coffee-api:latest` | `192.168.2.143:5050/gereon/coffee/coffee-api` |
| `coffee-dashboard:latest` | `192.168.2.143:5050/gereon/coffee/coffee-dashboard` |

Build: `./build.sh all` (oder `api` / `dashboard` einzeln)

---

## Konfiguration

### Docker Compose (Portainer)
```yaml
version: "3.8"

services:
  coffee-api:
    image: 192.168.2.143:5050/gereon/coffee/coffee-api:latest
    container_name: coffee-api
    restart: unless-stopped
    ports:
      - "8089:8080"
    volumes:
      - /volume2/docker_ssd/coffee-data:/app/data
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Data Source=/app/data/coffee.db"
      ApiKey: <secret>

  coffee-dashboard:
    image: 192.168.2.143:5050/gereon/coffee/coffee-dashboard:latest
    container_name: coffee-dashboard
    restart: unless-stopped
    ports:
      - "8090:80"
    depends_on:
      - coffee-api
```

---

## Test-Abdeckung

| Bereich | Status | Details |
|---------|--------|---------|
| SnapshotService | Keine Tests | Idempotenz, Cross-Day Delta |
| StatsController | Keine Tests | Daily, Range, Heatmap |
| IngestController | Keine Tests | Payload-Parsing, Auth |
| CoffeeTest/ (alt) | Obsolet | Nivona/MongoDB - kompiliert nicht mehr |

**Handlungsbedarf:** Alte Tests in CoffeeTest/ durch neue EQ900-Tests ersetzen.

---

## Aenderungshistorie

| Datum | Aenderung |
|-------|----------|
| 2025-01-25 | Projekt-Pivot: Nivona -> EQ900, MongoDB -> SQLite |
| 2025-01-25 | Core API implementiert (Ingest, Stats, Idempotenz) |
| 2025-01-25 | Deployment: Docker Registry, Portainer |
| 2025-01-25 | n8n Integration: Workflow laeuft produktiv |
| 2025-01-25 | MongoDB komplett entfernt |
| 2026-02-07 | React Dashboard: KPI, Charts, Heatmap, Peaks, Wochentage |
| 2026-02-07 | Backend: Cross-Day Delta Fix (Daily + Range) |
| 2026-02-07 | Gesamt-Ansicht mit absoluten Countern |
| 2026-02-07 | build.sh: Lokaler Docker Build ersetzt CI Stage |
| 2026-02-07 | Dashboard-Container (nginx) deployed |
| 2026-02-07 | CoffeeWeb (Blazor) entfernt, Solution bereinigt |
