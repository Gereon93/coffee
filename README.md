# Coffee Analytics Hub

Tracking und Visualisierung des Kaffeekonsums einer Siemens EQ900 Kaffeemaschine.

Die Maschine liefert per BSH Home Connect API Zaehlerstaende (Kaffee, Milch, Heisswasser, etc.), die alle 15 Minuten ueber n8n abgerufen und in einer SQLite-Datenbank gespeichert werden. Ein React-Dashboard zeigt Verbrauch, Trends und Muster an.

> **Hintergrund — vom Screenshot-OCR zur API-Integration:** Das Projekt
> startete 2023 mit einer *Nivona*-Maschine, die keine offene Schnittstelle
> bietet. Die Zaehlerstaende wurden damals per **Tesseract-OCR aus
> App-Screenshots** extrahiert — fragil und wartungsintensiv. Mit dem Umstieg
> auf eine Siemens EQ900 (Teil des BSH/Home-Connect-Oekosystems) wurde Anfang
> 2026 der gesamte OCR-Pfad durch eine saubere **API-Integration** ersetzt und
> der Storage von MongoDB auf SQLite + EF Core umgestellt. Die alte Loesung
> lebt nur noch in der Git-History.

## Architektur

```
EQ900 ──> Home Connect API ──> n8n (alle 15 Min) ──> Coffee API ──> SQLite
                                                          │
                                                    Coffee Dashboard
                                                    (React + nginx)
```

> **Ausfuehrliche Architekturdokumentation:** [`doc/arc42/`](doc/arc42/) —
> nach arc42 gegliedert, mit Kontext, Bausteinsicht, Laufzeitszenarien,
> Deployment, Querschnittskonzepten, ADRs und dem aktuellen Stand an
> technischen Schulden ([11-risks.md](doc/arc42/11-risks.md)).

| Komponente | Technologie | Port |
|------------|-------------|------|
| Coffee API | ASP.NET Core (.NET 10), SQLite, EF Core | 8089 |
| Coffee Dashboard | React 19, Vite, Recharts, Tailwind CSS v4 | 8090 |
| Scheduler | n8n (externer Workflow) | - |
| Hosting | Docker auf Synology NAS via Portainer | - |

**Design-Entscheidung — n8n als Cloud-Gateway:** Die Coffee API und das
Dashboard laufen bewusst **nur im lokalen Netz** und sind nicht aus dem
Internet erreichbar. Einzig n8n spricht mit der Home Connect Cloud — es pollt
die Zaehlerstaende und schiebt sie per POST in die LAN-API (Pull-Prinzip statt
offenem Port). Auch die umgekehrte Richtung (Maschine ein-/ausschalten) laeuft
ueber einen n8n-Webhook als Relay. Vorteile:

- Kein Port-Forwarding, kein Reverse-Proxy, keine Angriffsflaeche am NAS
- Die BSH/Home-Connect-Credentials (OAuth-Tokens) leben ausschliesslich in
  n8n — die API selbst kennt sie nicht
- Scheduling, Retries und Token-Refresh sind n8n-Aufgaben und halten die API
  schlank

## Voraussetzungen

- **Docker** (fuer Deployment)
- **.NET 10 SDK** (fuer lokale Entwicklung der API)
- **Node.js 22+** (fuer lokale Entwicklung des Dashboards)
- **n8n** oder anderer Scheduler (fuer die Datenerfassung)

## Schnellstart

### Docker Deployment (Produktion)

**Container-Images** werden automatisch von **GitHub Actions** gebaut und in die GitHub Container Registry (GHCR) gepusht, sobald auf `main` gemerget wird:

- `ghcr.io/gereon93/coffee-api:latest`
- `ghcr.io/gereon93/coffee-dashboard:latest`

Zusaetzlich wird jeder Build mit `:sha-<short-sha>` getaggt fuer Rollbacks.

**Fallback fuer lokale Builds** (wenn die CI nicht verfuegbar ist):

```bash
./build.sh all           # Baut API + Dashboard lokal, pusht zur Registry
./build.sh api           # Nur API
./build.sh dashboard     # Nur Dashboard
./build.sh api --no-push # Nur bauen, nicht pushen
```

**Docker Compose in Portainer deployen:**

```yaml
services:
  coffee-api:
    image: ghcr.io/gereon93/coffee-api:latest
    container_name: coffee-api
    restart: unless-stopped
    ports:
      - "8089:8080"
    volumes:
      - /path/to/coffee-data:/app/data
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Data Source=/app/data/coffee.db"
      ApiKey: <dein-api-key>
      # Ohne PowerWebhookUrl antworten /coffee/status und /coffee/power mit 500
      N8n__PowerWebhookUrl: "https://n8n.example.local/webhook/coffee-power"
      N8n__BasicAuthUser: <optional>
      N8n__BasicAuthPassword: <optional>
      SENTRY_DSN: <optional, leer = Error-Tracking aus>
      SENTRY_ENVIRONMENT: production

  coffee-dashboard:
    image: ghcr.io/gereon93/coffee-dashboard:latest
    container_name: coffee-dashboard
    restart: unless-stopped
    ports:
      - "8090:80"
    depends_on:
      - coffee-api
```

**Wichtig:** Das Volume `/path/to/coffee-data` speichert die SQLite-Datenbank persistent. Ohne dieses Volume gehen Daten bei Container-Neustarts verloren.

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
# Proxy leitet /api/* an http://localhost:8089 weiter
# (ueberschreibbar via VITE_API_PROXY_TARGET, konfiguriert in vite.config.ts)
```

> **Bekannte Einschraenkung:** Der Dev-Proxy deckt nur `/api` ab. `/coffee/status`
> und `/coffee/power` laufen unter `npm run dev` ins Leere — Power-Button und
> Live-Status funktionieren lokal daher nicht ohne Zusatzkonfiguration.
> Siehe [TD-10](doc/arc42/11-risks.md#correctness).

**Tests:**

```bash
dotnet test CoffeeTest/
# 87 Tests: Idempotenz, Cross-Day Deltas, Controller, Heatmap, Power, HomeConnect, Integration

cd coffee-dashboard && npm run test
# 102 Tests: lib/api/hooks, Charts, Modals, Power-Button, Seiten
```

## API Endpoints

| Method | Endpoint | Beschreibung | Auth |
|--------|----------|--------------|------|
| POST | `/api/ingest` | Snapshot von n8n entgegennehmen | API-Key |
| GET | `/api/stats?page=&pageSize=` | Alle Snapshots (paginiert, pageSize max. 100) | - |
| GET | `/api/stats/daily/{date}?tz=` | Tagesstatistik inkl. Baseline-Snapshot des Vortags | - |
| GET | `/api/stats/range?from=&to=&tz=` | Zeitraum-Aggregation pro lokalem Tag | - |
| GET | `/api/stats/heatmap?weeks=&tz=` | Heatmap-Daten (Wochentag x Stunde), weeks max. 52 | - |
| GET | `/api/stats/marked-days?kind=` | Markierte Tage, optional gefiltert nach `mass-import` / `event` | - |
| POST | `/api/stats/marked-days` | Tag markieren (`mass-import` oder `event`) | - |
| DELETE | `/api/stats/marked-days/{date}` | Markierung aufheben | - |
| GET | `/coffee/status` | Live-Status der Maschine (7s Server-Cache) | - |
| POST | `/coffee/power` | Maschine ein-/ausschalten (`{"state":"on"\|"off"}`) | - |
| GET | `/api/health` | Health Check inkl. `lastSnapshot` | - |
| GET | `/scalar/v1` | Interaktive API-Dokumentation | - |

Der `tz`-Parameter ist der UTC-Offset des Clients **in Minuten** (60 = CET,
120 = CEST). Das Frontend haengt ihn automatisch an. Ohne Angabe wird UTC
verwendet. Vollstaendiger Contract: [`SPEC.md`](SPEC.md).

> **Hinweis zur Absicherung:** Nur `/api/ingest` ist per API-Key geschuetzt.
> Die schreibenden Endpunkte `/coffee/power` und `/api/stats/marked-days`
> sind offen — tragbar nur unter der LAN-only-Annahme.
> Siehe [TD-01](doc/arc42/11-risks.md#security).
>
> CORS erlaubt nur die Origins aus `Cors:AllowedOrigins` (Umgebungsvariable
> `Cors__AllowedOrigins__0`). In `Development` gelten ohne Konfiguration
> `http://localhost:5173` und `http://localhost:8090`; in `Production` ist die
> Liste ohne Konfiguration leer und Cross-Origin-Zugriff damit komplett
> gesperrt. Das Dashboard braucht das nicht — es ruft `/api` relativ ueber den
> nginx-Proxy auf, also same-origin. Nur wer die API direkt von einer anderen
> Origin anspricht, setzt `Cors__AllowedOrigins__0` (exakte Origin, ohne Pfad
> und ohne Slash am Ende).

### Authentifizierung

Der Ingest-Endpoint ist per API-Key geschuetzt. Der Key wird als `ApiKey` Environment-Variable im Container gesetzt und muss als `X-API-Key` Header mitgeschickt werden (Vergleich erfolgt konstantzeitig):

```bash
curl -X POST http://coffee.example.local:8089/api/ingest \
  -H "Content-Type: application/json" \
  -H "X-API-Key: <dein-key>" \
  -d '{"data":{"status":[{"key":"ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee","value":42}]}}'
```

### Idempotenz

Der Ingest-Endpoint ist idempotent: Wenn sich seit dem letzten Snapshot kein Zaehler erhoeht hat, wird kein Duplikat angelegt (HTTP 200 statt 201, mit der ID des bestehenden Snapshots). So kann n8n bedenkenlos alle 15 Minuten senden.

Konkret: gespeichert wird nur, wenn mindestens einer der Getraenke-Zaehler
(Kaffee, Kaffee+Milch, Milch, Heisswasser-Tassen) **groesser** ist als im
letzten Snapshot. Reine Status-Aenderungen landen nicht in der DB — der
Live-Zustand kommt stattdessen von `/coffee/status`.

Ist der `ApiKey` nicht gesetzt, laesst die Middleware die Anfrage mit einer
Warnung im Log durch. In Produktion also zwingend setzen.

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
| Massenimport-Markierung | Tage ueber Log-Tab als Backfill markieren, grau mit Label im Chart, aus Heatmap und Anomalie-Erkennung ausgeschlossen |
| Event-Markierung | Tage mit Anlass annotieren (Geburtstag, Besuch, Party, krank, Urlaub, sonstiges) — bleiben voll in der Statistik |
| Power-Steuerung | Maschine aus dem Dashboard ein-/ausschalten, gesperrt ausserhalb 07:00-18:00 (Europe/Berlin) |
| Live-Status | Aktueller Maschinenzustand, degradiert zu "Offline" statt zu einem Fehler |

## Projektstruktur

```
coffee/
├── CoffeeApi/              # ASP.NET Core Backend
│   ├── Controllers/        #   Ingest, Stats, MarkedDays, Power, CoffeeStatus
│   ├── Domain/             #   MachineSnapshot, MarkedDay
│   ├── DTOs/               #   Request/Response Objekte (Entities bleiben intern)
│   ├── Infrastructure/     #   AppDbContext, MigrationBaseliner
│   ├── Middleware/         #   API-Key Authentication
│   ├── Migrations/         #   EF Core Migrations
│   ├── Services/           #   SnapshotService, MarkedDayService, HomeConnectService
│   └── Dockerfile
├── coffee-dashboard/       # React Frontend
│   ├── src/
│   │   ├── api/            #   API Client + Fetch-Funktionen + Typen
│   │   ├── components/     #   Charts, Cards, Controls, Layout, Modals
│   │   ├── hooks/          #   TanStack-Query-Hooks pro Endpunkt
│   │   ├── lib/            #   Pure Utilities (Datum, Anomalien, Time-Lock, Sentry)
│   │   └── pages/          #   Dashboard, Heatmap, Log
│   ├── nginx.conf          #   SPA Routing + API Proxy
│   └── Dockerfile
├── CoffeeTest/             # 87 Unit-, Controller- und Integrationstests
│   ├── Controllers/        #   Alle fuenf Controller, jeder Branch
│   ├── Domain/             #   MachineSnapshot
│   ├── Helpers/            #   TestDbContextFactory, SnapshotBuilder, StubHttpMessageHandler
│   ├── Infrastructure/     #   MigrationBaseliner gegen echte SQLite-Dateien
│   ├── Integration/        #   WebApplicationFactory, voller HTTP-Stack
│   └── Services/           #   SnapshotService, HomeConnectService
├── doc/arc42/              # Architekturdokumentation nach arc42
├── .github/workflows/      # ci, docker-publish, sonar
├── build.sh                # Docker Build + Push Script (Podman/Docker)
├── Coffee.sln              # .NET Solution
└── SPEC.md                 # API-Contract
```

## CI/CD

| Workflow | Trigger | Zweck |
|----------|---------|-------|
| `ci.yml` | Push auf `main`/`dev`, jeder PR | Job `test`: `dotnet restore` + `build -c Release` + `dotnet test`. Job `dashboard`: `npm ci` + `lint` + `test` + `build` |
| `sonar.yml` | Push auf `main` | SonarQube-Scan inkl. Coverage (No-Op ohne `SONAR_*`-Secrets) |
| `docker-publish.yml` | Push auf `main`, manuell | Baut beide Images und pusht nach GHCR |

Der Sonar-Job sammelt beide Coverage-Reports ein und meldet sie an SonarQube:
OpenCover fuer `CoffeeApi` (`sonar.cs.opencover.reportsPaths`) und lcov fuer
`coffee-dashboard` (`sonar.javascript.lcov.reportPaths`).

## n8n Workflow

Der n8n Workflow laeuft als Cron Job alle 15 Minuten (07:00-02:00):

1. **HTTP Request** an Home Connect API → holt aktuelle Zaehlerstaende der EQ900
2. **HTTP Request** POST an `/api/ingest` → sendet Snapshot an Coffee API

Die API erkennt Duplikate automatisch - wenn sich die Zaehler nicht geaendert haben, wird kein neuer Eintrag angelegt.

## Tests

87 Tests decken die Kernlogik ab — Services, Controller (jeder Branch), Domain und Infrastruktur:

| Testklasse | Tests | Bereich |
|------------|-------|---------|
| SnapshotServiceIdempotencyTests | 5 | First Snapshot, Duplicate Skip, Counter Increase |
| SnapshotServiceDailySummaryTests | 7 | Cross-Day Delta, Peak Hour, Baseline |
| SnapshotServiceQueryTests | 7 | GetLatest, Pagination, GetByDate/Range |
| SnapshotServiceHeatmapTests | 5 | DayOfWeek Grouping, Sunday=7 (ISO-8601) |
| HomeConnectServiceTests | 11 | Power-Webhook, Status-Parsing, Timeout/Netzwerkfehler, Basic-Auth |
| IngestControllerTests | 4 | Null/Empty Validation, 201 Created, 200 Duplicate |
| StatsControllerTests | 13 | Range Aggregation, Health, Heatmap Cap, Datumsformat |
| MarkedDaysControllerTests | 15 | CRUD, Validierung, Event-Typen, Edge-Cases |
| PowerControllerTests | 7 | On/Off, ungueltiger State (400, 4 Faelle), Service-Fehler (500) |
| CoffeeStatusControllerTests | 3 | Payload, Caching (TTL), Unreachable-Passthrough |
| MachineSnapshotTests | 3 | TotalBeverages, Default Values |
| MigrationBaselinerTests | 3 | EF Migration History Baselining |
| ApiIntegrationTests | 4 | Voller HTTP-Stack gegen echte SQLite: Health, Stats, API-Key 401/200 |

```bash
dotnet test CoffeeTest/
```

## Datensicherung

Die gesamte Datenhaltung liegt in einer einzigen SQLite-Datei:

```
NAS: /path/to/coffee-data/coffee.db
```

Fuer ein Backup reicht es, diese Datei zu kopieren. Die DB wird per Docker Volume in den API-Container gemountet und ueberlebt Container-Updates.

## Error-Tracking

Errors gehen an https://glitchtip.example.com (self-hosted GlitchTip, Sentry-API-kompatibel). DSN aus `Project -> Settings -> Client Keys` in der GlitchTip-UI holen und in die jeweilige `.env` eintragen:

- Backend: `CoffeeApi/.env.example` zeigt die ENV-Variablen (`SENTRY_DSN`, `SENTRY_ENVIRONMENT`, `SENTRY_RELEASE`, `SENTRY_TRACES_SAMPLE_RATE`). Werden zur Laufzeit aus der Container-Env gelesen.
- Frontend: `coffee-dashboard/.env` enthaelt `VITE_SENTRY_DSN`, `VITE_SENTRY_ENVIRONMENT`, `VITE_SENTRY_RELEASE`, `VITE_SENTRY_TRACES_SAMPLE_RATE`. Build-Time-Variablen — also vor `npm run build` setzen.

Leerer DSN = Sentry komplett deaktiviert, kein Netzwerk-Call. Damit kann lokal ohne Anbindung entwickelt werden, ohne dass Test-Errors die Live-Instanz fluten.

## Schema-Migrationen

Das Backend nutzt **EF Core Migrations**. Beim Container-Start wird sequentiell ausgefuehrt:

1. `MigrationBaseliner.EnsureBaselined()` — erkennt automatisch Pre-Migration-DBs (z.B. die urspruengliche Prod-DB von der NAS, die mit `EnsureCreated()` angelegt wurde) und seedet `__EFMigrationsHistory` mit der Initial-Migration, sodass keine Tabelle doppelt angelegt wird.
2. `Database.Migrate()` — wendet alle pending Migrations an, bestehende Daten bleiben unberuehrt.

### Neue Migration anlegen

```bash
cd CoffeeApi
dotnet ef migrations add <NameDerAenderung>
```

Beim naechsten Deploy laeuft sie beim Container-Start automatisch. Kein manueller SQL-Schritt, kein SSH noetig.

### Tests und Migrations

Tests in `CoffeeTest/` nutzen `InMemoryDatabase` — kein Migration-Support. `TestDbContextFactory.Create()` nutzt weiterhin `EnsureCreated()`. Der `MigrationBaseliner` wird separat gegen eine temporaere SQLite-Datei getestet (`MigrationBaselinerTests`).
