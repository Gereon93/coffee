# Coffee Analytics Hub

Tracking und Visualisierung des Kaffeekonsums einer Philips EQ900 Kaffeemaschine.

Die Maschine liefert per Home Connect API Zaehlerstaende (Kaffee, Milch, Heisswasser, etc.), die alle 15 Minuten ueber n8n abgerufen und in einer SQLite-Datenbank gespeichert werden. Ein React-Dashboard zeigt Verbrauch, Trends und Muster an.

## Architektur

```
EQ900 ──> Home Connect API ──> n8n (alle 15 Min) ──> Coffee API ──> SQLite
                                                          │
                                                    Coffee Dashboard
                                                    (React + nginx)
```

| Komponente | Technologie | Port |
|------------|-------------|------|
| Coffee API | ASP.NET Core (.NET 10), SQLite, EF Core | 8089 |
| Coffee Dashboard | React 19, Vite, Recharts, Tailwind CSS v4 | 8090 |
| Scheduler | n8n (externer Workflow) | - |
| Hosting | Docker auf Synology NAS via Portainer | - |

## Voraussetzungen

- **Docker** (fuer Deployment)
- **.NET 10 SDK** (fuer lokale Entwicklung der API)
- **Node.js 22+** (fuer lokale Entwicklung des Dashboards)
- **n8n** oder anderer Scheduler (fuer die Datenerfassung)

## Schnellstart

### Docker Deployment (Produktion)

**Container-Images** werden automatisch von der GitLab-Pipeline gebaut und gepusht, sobald auf `main` gemerget wird:

- `192.168.2.143:5050/gereon/coffee/coffee-api:latest`
- `192.168.2.143:5050/gereon/coffee/coffee-dashboard:latest`

Zusaetzlich wird jeder Build mit `:${CI_COMMIT_SHORT_SHA}` getaggt fuer Rollbacks.

**Fallback fuer lokale Builds** (wenn die CI nicht verfuegbar ist):

```bash
./build.sh all           # Baut API + Dashboard lokal, pusht zur Registry
./build.sh api           # Nur API
./build.sh dashboard     # Nur Dashboard
./build.sh api --no-push # Nur bauen, nicht pushen
```

**Docker Compose in Portainer deployen:**

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
      ApiKey: <dein-api-key>

  coffee-dashboard:
    image: 192.168.2.143:5050/gereon/coffee/coffee-dashboard:latest
    container_name: coffee-dashboard
    restart: unless-stopped
    ports:
      - "8090:80"
    depends_on:
      - coffee-api
```

**Wichtig:** Das Volume `/volume2/docker_ssd/coffee-data` speichert die SQLite-Datenbank persistent. Ohne dieses Volume gehen Daten bei Container-Neustarts verloren.

### Lokale Entwicklung

**API:**

```bash
cd CoffeeApi
dotnet run
# Laeuft auf http://localhost:5000
# API-Doku: http://localhost:5000/scalar/v1
```

**Dashboard:**

```bash
cd coffee-dashboard
npm install
npm run dev
# Laeuft auf http://localhost:5173
# Proxy leitet /api/* an die NAS-API weiter (konfiguriert in vite.config.ts)
```

**Tests:**

```bash
dotnet test CoffeeTest/
# 33 Tests: Idempotenz, Cross-Day Deltas, Controller, Heatmap
```

## API Endpoints

| Method | Endpoint | Beschreibung | Auth |
|--------|----------|--------------|------|
| POST | `/api/ingest` | Snapshot von n8n entgegennehmen | API-Key |
| GET | `/api/stats` | Alle Snapshots (paginiert) | - |
| GET | `/api/stats/daily/{date}` | Tagesstatistik mit Snapshots | - |
| GET | `/api/stats/range?from=&to=` | Zeitraum-Aggregation | - |
| GET | `/api/stats/heatmap?weeks=4` | Heatmap-Daten (Wochentag x Stunde) | - |
| GET | `/api/health` | Health Check | - |
| GET | `/scalar/v1` | Interaktive API-Dokumentation | - |

### Authentifizierung

Der Ingest-Endpoint ist per API-Key geschuetzt. Der Key wird als `ApiKey` Environment-Variable im Container gesetzt und muss als `X-Api-Key` Header mitgeschickt werden:

```bash
curl -X POST http://192.168.2.143:8089/api/ingest \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: <dein-key>" \
  -d '{"data":{"status":[{"key":"ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee","value":42}]}}'
```

### Idempotenz

Der Ingest-Endpoint ist idempotent: Wenn ein Snapshot mit identischen Zaehlern bereits existiert, wird kein Duplikat angelegt (HTTP 200 statt 201). So kann n8n bedenkenlos alle 15 Minuten senden.

## Dashboard Features

| Feature | Beschreibung |
|---------|--------------|
| KPI Cards | Heute-Statistik + Zeitraum-Zusammenfassung |
| Period Selector | Woche, Monat, Jahr, Gesamt |
| Gesamt-Ansicht | Absolute Counter seit Inbetriebnahme der EQ900 |
| Taeglicher Verbrauch | Stacked Bar Chart (Kaffee + Milch pro Tag) |
| Verbrauchs-Trend | Area Chart ueber den gewaehlten Zeitraum |
| Verteilung | Pie Chart Kaffee vs. Milch vs. Heisswasser |
| Heutige Peaks | Stuendliche Verbrauchsspitzen |
| Wochentag-Vergleich | Durchschnittlicher Verbrauch pro Wochentag |
| Heatmap | Wochentag x Stunde Matrix |
| Anomalie-Erkennung | Z-Score basiert, markiert ungewoehnliche Tage |
| Dark Mode | Automatisch nach System Preference |

## Projektstruktur

```
coffee/
├── CoffeeApi/              # ASP.NET Core Backend
│   ├── Controllers/        #   API Endpoints (Ingest, Stats)
│   ├── Domain/             #   MachineSnapshot Entity
│   ├── DTOs/               #   Request/Response Objekte
│   ├── Infrastructure/     #   AppDbContext (EF Core + SQLite)
│   ├── Middleware/          #   API-Key Authentication
│   ├── Services/           #   SnapshotService (Geschaeftslogik)
│   └── Dockerfile
├── coffee-dashboard/       # React Frontend
│   ├── src/
│   │   ├── api/            #   API Client + Fetch-Funktionen
│   │   ├── components/     #   Charts, Cards, Controls, Layout
│   │   ├── hooks/          #   React Query Hooks
│   │   ├── lib/            #   Utilities (Datum, Formatierung)
│   │   └── pages/          #   Dashboard + Heatmap Page
│   ├── nginx.conf          #   SPA Routing + API Proxy
│   └── Dockerfile
├── CoffeeTest/             # Unit + Integration Tests
│   ├── Controllers/        #   Ingest + Stats Controller Tests
│   ├── Domain/             #   MachineSnapshot Tests
│   ├── Helpers/            #   TestDbContextFactory, SnapshotBuilder
│   └── Services/           #   SnapshotService Tests
├── build.sh                # Docker Build + Push Script
├── Coffee.sln              # .NET Solution
└── PROJECT_STATE.md        # Projektstatus + Aenderungshistorie
```

## n8n Workflow

Der n8n Workflow laeuft als Cron Job alle 15 Minuten (07:00-02:00):

1. **HTTP Request** an Home Connect API → holt aktuelle Zaehlerstaende der EQ900
2. **HTTP Request** POST an `/api/ingest` → sendet Snapshot an Coffee API

Die API erkennt Duplikate automatisch - wenn sich die Zaehler nicht geaendert haben, wird kein neuer Eintrag angelegt.

## Tests

33 Tests decken die Kernlogik ab:

| Testklasse | Tests | Bereich |
|------------|-------|---------|
| MachineSnapshotTests | 3 | TotalBeverages, Default Values |
| SnapshotServiceIdempotencyTests | 5 | First Snapshot, Duplicate Skip, Counter Increase |
| SnapshotServiceDailySummaryTests | 4 | Cross-Day Delta, Peak Hour, Baseline |
| SnapshotServiceQueryTests | 5 | GetLatest, Pagination, GetByDate/Range |
| SnapshotServiceHeatmapTests | 3 | DayOfWeek Grouping, Sunday=7 (ISO-8601) |
| IngestControllerTests | 4 | Null/Empty Validation, 201 Created, 200 Duplicate |
| StatsControllerTests | 6 | Range Aggregation, Health, Heatmap Cap |

```bash
dotnet test CoffeeTest/
```

## Datensicherung

Die gesamte Datenhaltung liegt in einer einzigen SQLite-Datei:

```
NAS: /volume2/docker_ssd/coffee-data/coffee.db
```

Fuer ein Backup reicht es, diese Datei zu kopieren. Die DB wird per Docker Volume in den API-Container gemountet und ueberlebt Container-Updates.
