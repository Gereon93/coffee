# Project State: Coffee Analytics Hub

## Aktueller Fokus

**Phase:** Production - Datenerfassung läuft
**Status:** MVP ist live, sammelt Daten

---

## Meilensteine

| Phase | Status | Beschreibung |
|-------|--------|--------------|
| 1. Vision & Strategie | ✅ Fertig | Pivot zu EQ900/SQLite bestätigt |
| 2. Architecture & Spec | ✅ Fertig | ARCHITECTURE.md, DESIGN.md, SPEC.md |
| 3. Infrastructure | ✅ Fertig | .NET 10, SQLite, Scalar |
| 4. Core API | ✅ Fertig | Ingest + Stats Endpoints |
| 5. n8n Integration | ✅ Fertig | Workflow läuft, pusht alle 15 Min |
| 6. Read API | ✅ Fertig | Stats-Endpoints für Frontend |
| 7. Deployment | ✅ Fertig | Docker Registry, Portainer |
| 8. MongoDB Cleanup | ✅ Fertig | Legacy-Code entfernt |
| 9. React Frontend | ⏳ Offen | Dashboard, Charts |
| 10. CI/CD Pipeline | ⏳ Offen | Runner braucht .NET 10 |
| 11. Testing | ⏳ Offen | Unit + Integration Tests |

---

## Was läuft produktiv

| Komponente | Status | Details |
|------------|--------|---------|
| Coffee API | ✅ Live | `192.168.2.143:8089` |
| SQLite DB | ✅ Live | Persistiert in Docker Volume |
| n8n Workflow | ✅ Live | Cron alle 15 Min (07:00-02:00) |
| Idempotenz | ✅ Funktioniert | Duplikate werden übersprungen |
| Scalar UI | ✅ Live | `192.168.2.143:8089/scalar/v1` |
| API-Key Auth | ✅ Live | `/api/ingest` geschützt |

---

## Nächste Schritte (Priorisiert)

1. **Daten sammeln lassen** (paar Tage warten für sinnvolle Charts)

2. **GitLab Runner aktualisieren**
   - Neues Docker Image mit .NET 10 SDK
   - Dann läuft CI/CD Pipeline automatisch

3. **React Frontend** (wenn genug Daten)
   - Vite + TypeScript
   - Recharts für Visualisierung
   - Dashboard, Heatmap, Tagesvergleiche

4. **Unit Tests**
   - SnapshotService Idempotenz-Logik
   - Controller Tests

---

## Tech Stack (Final)

| Layer | Technologie |
|-------|-------------|
| Backend | ASP.NET Core (.NET 10) |
| Datenbank | SQLite + EF Core |
| API-Doku | Scalar |
| Scheduler | n8n (extern) |
| Auth | API-Key Header |
| Container | Docker |
| Registry | GitLab (192.168.2.143:5050) |
| Hosting | Portainer auf NAS |

---

## API Endpoints

| Method | Endpoint | Beschreibung |
|--------|----------|--------------|
| POST | `/api/ingest` | n8n liefert Snapshots (API-Key required) |
| GET | `/api/stats` | Alle Snapshots (paginiert) |
| GET | `/api/stats/daily/{date}` | Tagesstatistik |
| GET | `/api/stats/range?from=&to=` | Zeitraum |
| GET | `/api/stats/heatmap?weeks=4` | Heatmap-Daten |
| GET | `/api/health` | Health Check |
| GET | `/scalar/v1` | API Dokumentation |

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
      - coffee-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Default=Data Source=/app/data/coffee.db
      - ApiKey=<secret>

volumes:
  coffee-data:
    driver: local
```

### n8n HTTP Request
```
Method: POST
URL: http://192.168.2.143:8089/api/ingest
Headers:
  X-API-Key: <secret>
  Content-Type: application/json
```

---

## Änderungshistorie

| Datum | Änderung |
|-------|----------|
| 2025-01-25 | Projekt-Pivot: Nivona → EQ900, MongoDB → SQLite |
| 2025-01-25 | Core API implementiert (Ingest, Stats, Idempotenz) |
| 2025-01-25 | Deployment: Docker Registry, Portainer |
| 2025-01-25 | n8n Integration: Workflow läuft produktiv |
| 2025-01-25 | MongoDB komplett entfernt, sauberer Code |
