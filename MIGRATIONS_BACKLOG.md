# Migrations-Backlog (EQ900)

Status-Markierungen: [ ] offen, [~] in Arbeit, [x] erledigt.

## 0. Grundlagen und Entscheidungspunkte
- [x] Ziel-Architektur: Push via n8n + SQLite.
- [x] ARCHITECTURE.md mit Tech-Stack und Systemkontext.
- [x] DESIGN.md mit Datenmodell und Service-Logik.
- [x] SPEC.md mit API-Kontrakten und Idempotenz-Regeln.
- [x] PROJECT_STATE.md mit Pivot-Dokumentation.
- [x] .NET 10 Environment aufsetzen.
- [x] Scalar (OpenAPI) für Dokumentation einrichten.

## 1. Datenmodell & Persistenz (SQLite)
- [x] EF Core SQLite Packages installieren.
- [x] Domain Model `MachineSnapshot` erstellen.
- [x] `AppDbContext` einrichten.
- [x] Migration via `EnsureCreated()`.
- [x] Service-Layer: `SnapshotService` mit Idempotenz-Check.

## 2. API Ingest (Core Hub)
- [x] Controller `IngestController` anlegen.
- [x] Endpoint `POST /api/ingest` definieren.
- [x] DTOs für n8n-Payload erstellen.
- [x] Mapping DTO -> Domain Model.
- [x] Integration: Controller ruft `SnapshotService` auf.
- [x] API-Key Middleware für Authentifizierung.

## 3. n8n Workflow (Extern)
- [x] Home Connect Credentials in n8n hinterlegen.
- [x] HTTP Request: GET Home Connect Status.
- [x] HTTP Request: POST an CoffeeApi.
- [x] Trigger: Cron `*/15 7-2 * * *`.
- [x] Workflow läuft produktiv!

## 4. REST API (Read-Layer)
- [x] Endpunkte für Frontend (GET /stats, /stats/daily, /stats/range, /stats/heatmap).
- [x] Optimierte Queries (EF Core).
- [x] Health Check Endpoint (GET /api/health).

## 5. Deployment
- [x] Dockerfile aktualisiert (.NET 10).
- [x] Docker Image gebaut und gepusht.
- [x] GitLab Registry (192.168.2.143:5050).
- [x] Portainer Stack konfiguriert.
- [x] Container läuft auf NAS (Port 8089).

## 6. Cleanup
- [x] MongoDB komplett entfernt.
- [x] Alte Nivona Controller/Models/Repos gelöscht.
- [x] Legacy Dependencies entfernt (Tesseract, OpenAI, etc.).

## 7. CoffeeWeb (React) - OFFEN
- [ ] Vite Projekt mit TypeScript initialisieren.
- [ ] API Client generieren (via Scalar/OpenAPI).
- [ ] Dashboard View implementieren.
- [ ] Heatmap Visualisierung.
- [ ] Tagesvergleiche.

## 8. CI/CD Pipeline - OFFEN
- [ ] GitLab Runner Image auf .NET 10 aktualisieren.
- [ ] Pipeline testen.

## 9. Tests & Qualität - OFFEN
- [ ] Unit Tests für `SnapshotService` (Idempotenz-Logik).
- [ ] Integration Test: API nimmt JSON an und schreibt in SQLite.

## 10. Doku - OFFEN
- [ ] README aktualisieren (Setup, ENV, Runbook).
- [ ] Beispiel-Konfiguration dokumentieren.

---

## Definition of Done (MVP) ✅

- [x] 15-Minuten Job läuft stabil.
- [x] Daten landen idempotent in SQLite.
- [x] API-Key Authentifizierung funktioniert.
- [x] Health Check vorhanden.
- [x] Scalar UI verfügbar.
