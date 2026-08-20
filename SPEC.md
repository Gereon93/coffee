# API Specification: Coffee Analytics Hub

## Übersicht

Diese Spezifikation definiert die API-Kontrakte zwischen:
- **n8n** (Data Fetcher) → **CoffeeApi** (Ingest)
- **React Frontend** → **CoffeeApi** (Read, Markierungen, Power-Steuerung)
- **CoffeeApi** → **n8n-Webhook** (Power-Relay zur Home-Connect-Cloud)

Die interaktive Fassung steht unter `/scalar/v1`, das OpenAPI-Dokument unter
`/openapi/v1.json` — beides nur, wenn die API mit `ASPNETCORE_ENVIRONMENT=Development`
laeuft; in Produktion antworten beide mit `404`. Bei Abweichung gilt der Code.

### Zeitzonen-Parameter `tz`

Alle Statistik-Endpunkte, die nach lokalen Tagen gruppieren
(`/api/stats/daily/{date}`, `/api/stats/range`, `/api/stats/heatmap`),
akzeptieren `tz` — den UTC-Offset des Clients **in Minuten** (60 = CET,
120 = CEST). Ohne Angabe wird UTC verwendet. Das Frontend haengt den Wert
automatisch an.

### Bohnenfach-Zuordnung

Die EQ900 hat zwei Bohnenfaecher. Home Connect meldet kein Fach, aber getrennte
Zaehler — daraus leitet die API die Zuordnung ab. Ein Bezug existiert nur als
**Delta zwischen zwei aufeinanderfolgenden Snapshots**; das Delta wird dem
**spaeteren** der beiden Snapshots zugeschrieben.

Zwei Zaehler koennen Bohnen ziehen, und jeder hat ein Standardfach:

| `counter` | Zaehler | Standard-`beanHopper` |
|-----------|---------|-----------------------|
| `coffee` | Kaffee | `1` |
| `coffeeAndMilk` | K+Milch (Cappuccino, Latte macchiato) | `2` |

`Milch` und `Heisswasser` ziehen keine Bohnen und tauchen in `beanHoppers` nie
auf. Ein Zaehler ohne Standardfach faellt auf `1` zurueck.

Ein Snapshot-Delta kann gemischt sein — zwei Kaffee **und** ein K+Milch im selben
Intervall. `beanHoppers` ist deshalb eine Liste mit je einem Eintrag pro bewegtem
Zaehler, und eine manuelle Korrektur (Endpunkte 9 und 10) gilt jeweils fuer genau
ein `(Snapshot, counter)`-Paar. `source` sagt, woher die Zuordnung stammt:
`auto` = Standardregel, `manual` = gespeicherte Korrektur.

`beanHopper: null` heisst "kein Bohnenverbrauch" — die Bezuege zaehlen dann in
den Aggregaten unter `excluded` statt unter `hopper1`/`hopper2`.

Gramm-Werte, Bohnensorten und Inventar kennt diese API nicht; sie liefert nur
die Bezugsmengen je Fach. Die Bewertung liegt im Dashboard (Murgbyte/dashboard-s7#235).

---

## Base URL

| Environment | URL |
|-------------|-----|
| Development | `http://localhost:5000` — so startet `dotnet run`. Der Vite-Dev-Server proxied `/api` per Default auf `http://localhost:8089`; fuer den lokal laufenden `dotnet run` also `VITE_API_PROXY_TARGET=http://localhost:5000` setzen. |
| Production | `http://coffee.example.local:8089` — Container-Port der API. Das Dashboard liegt auf `:8090` und proxied `/api` und `/coffee` per nginx an die API weiter. |

---

## Endpoints

### 1. POST /api/ingest

**Zweck:** Empfängt EQ900-Snapshots von n8n

#### Request

```http
POST /api/ingest HTTP/1.1
Content-Type: application/json

{
  "data": {
    "status": [
      { "key": "BSH.Common.Status.OperationState", "value": "BSH.Common.EnumType.OperationState.Ready" },
      { "key": "BSH.Common.Status.RemoteControlStartAllowed", "value": true },
      { "key": "BSH.Common.Status.LocalControlActive", "value": false },
      { "key": "BSH.Common.Status.InteriorIlluminationActive", "value": false },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "value": 988 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", "value": 10 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", "value": 11 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", "value": 1 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater", "value": 150, "unit": "ml" }
    ]
  }
}
```

#### Response (201 Created - Neuer Snapshot)

```json
{
  "id": 42,
  "created": true,
  "timestamp": "2025-01-25T10:15:00Z",
  "message": "Snapshot created"
}
```

#### Response (200 OK - Duplikat/Keine Änderung)

```json
{
  "id": 41,
  "created": false,
  "timestamp": "2025-01-25T10:00:00Z",
  "message": "No counter increase detected, snapshot skipped"
}
```

#### Response (400 Bad Request)

```json
{
  "error": "Invalid payload",
  "details": ["data.status is required"]
}
```

---

### 2. GET /api/stats

**Zweck:** Alle Snapshots abrufen (paginiert)

#### Request

```http
GET /api/stats?page=1&pageSize=50 HTTP/1.1
```

#### Query Parameters

| Parameter | Type | Default | Beschreibung |
|-----------|------|---------|--------------|
| page | int | 1 | Seitennummer |
| pageSize | int | 50 | Einträge pro Seite (max 100) |

#### Response (200 OK)

```json
{
  "data": [
    {
      "id": 42,
      "timestamp": "2025-01-25T10:15:00Z",
      "beverageCounterCoffee": 988,
      "beverageCounterCoffeeAndMilk": 10,
      "beverageCounterMilk": 11,
      "totalBeverages": 1009,
      "operationState": "Ready",
      "beanHoppers": [
        { "counter": "coffee", "count": 2, "beanHopper": 1, "source": "auto" },
        { "counter": "coffeeAndMilk", "count": 1, "beanHopper": 2, "source": "manual" }
      ]
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalItems": 1234,
    "totalPages": 25
  }
}
```

---

### 3. GET /api/stats/daily/{date}

**Zweck:** Tagesstatistik abrufen

#### Request

```http
GET /api/stats/daily/2025-01-25 HTTP/1.1
```

#### Path Parameters

| Parameter | Type | Format | Beschreibung |
|-----------|------|--------|--------------|
| date | string | yyyy-MM-dd | Datum |

#### Query Parameters

| Parameter | Type | Default | Beschreibung |
|-----------|------|---------|--------------|
| tz | int | 0 | UTC-Offset des Clients in Minuten |

Die Antwort enthaelt als ersten Eintrag in `snapshots` den letzten Snapshot des
Vortags als Baseline, damit das erste Stunden-Delta korrekt berechenbar ist.
Dieser Baseline-Eintrag hat ein leeres `beanHoppers`, weil sein eigenes Delta zum
Vortag gehoert.

#### Response (200 OK)

```json
{
  "date": "2025-01-25",
  "snapshots": [
    {
      "id": 41,
      "timestamp": "2025-01-25T07:00:00Z",
      "beverageCounterCoffee": 980,
      "totalBeverages": 1000,
      "beanHoppers": []
    },
    {
      "id": 42,
      "timestamp": "2025-01-25T07:15:00Z",
      "beverageCounterCoffee": 982,
      "totalBeverages": 1002,
      "beanHoppers": [
        { "counter": "coffee", "count": 2, "beanHopper": 1, "source": "auto" }
      ]
    }
  ],
  "summary": {
    "coffeeToday": 8,
    "milkDrinksToday": 2,
    "totalToday": 10,
    "peakHour": 9,
    "beanHoppers": { "hopper1": 8, "hopper2": 2, "excluded": 0 }
  }
}
```

---

### 4. GET /api/stats/range

**Zweck:** Snapshots in Zeitraum abrufen

#### Request

```http
GET /api/stats/range?from=2025-01-20&to=2025-01-25 HTTP/1.1
```

#### Query Parameters

| Parameter | Type | Format | Required | Beschreibung |
|-----------|------|--------|----------|--------------|
| from | string | yyyy-MM-dd | Ja | Startdatum |
| to | string | yyyy-MM-dd | Ja | Enddatum |
| tz | int | - | Nein | UTC-Offset des Clients in Minuten (Default 0) |

Tage ohne Snapshots fehlen in `data[]` — sie erscheinen nicht mit Nullwerten.

#### Response (200 OK)

```json
{
  "from": "2025-01-20",
  "to": "2025-01-25",
  "data": [
    {
      "date": "2025-01-20",
      "coffeeCount": 45,
      "milkCount": 5,
      "total": 50,
      "beanHoppers": { "hopper1": 45, "hopper2": 3, "excluded": 2 }
    },
    {
      "date": "2025-01-21",
      "coffeeCount": 42,
      "milkCount": 8,
      "total": 50,
      "beanHoppers": { "hopper1": 42, "hopper2": 8, "excluded": 0 }
    }
  ]
}
```

---

### 5. GET /api/stats/heatmap

**Zweck:** Aggregierte Daten für Heatmap (Stunde x Wochentag)

#### Request

```http
GET /api/stats/heatmap?weeks=4 HTTP/1.1
```

#### Query Parameters

| Parameter | Type | Default | Beschreibung |
|-----------|------|---------|--------------|
| weeks | int | 4 | Anzahl Wochen zurück, gedeckelt auf 52 |
| tz | int | 0 | UTC-Offset des Clients in Minuten |

#### Response (200 OK)

```json
{
  "weeks": 4,
  "heatmap": [
    { "dayOfWeek": 1, "hour": 7, "count": 12 },
    { "dayOfWeek": 1, "hour": 8, "count": 25 },
    { "dayOfWeek": 1, "hour": 9, "count": 45 },
    { "dayOfWeek": 2, "hour": 7, "count": 10 }
  ]
}
```

**Hinweis:** `dayOfWeek` folgt ISO-8601 (1 = Montag, 7 = Sonntag)

---

### 6. GET /api/stats/marked-days

**Zweck:** Manuell markierte Tage lesen

#### Query Parameters

| Parameter | Type | Required | Beschreibung |
|-----------|------|----------|--------------|
| kind | string | Nein | Filter: `mass-import` oder `event` |

#### Response (200 OK)

```json
[
  {
    "date": "2026-04-18",
    "kind": "mass-import",
    "eventType": null,
    "reason": "BSH API Ausfall",
    "createdAt": "2026-04-19T08:12:00Z"
  },
  {
    "date": "2026-04-22",
    "kind": "event",
    "eventType": "birthday",
    "reason": "Geburtstag",
    "createdAt": "2026-04-22T06:30:00Z"
  }
]
```

`kind` bestimmt die Semantik: `mass-import`-Tage werden aus Aggregaten,
Heatmap und Anomalie-Erkennung ausgeschlossen; `event`-Tage bleiben in der
Statistik und sind nur von der Anomalie-Erkennung ausgenommen.
`eventType` ist bei `kind=event` Pflicht und einer von
`birthday` | `visitors` | `party` | `sick` | `vacation` | `other`.

---

### 7. POST /api/stats/marked-days

**Zweck:** Tag markieren

**Auth:** `X-API-Key` erforderlich.

#### Request

```json
{
  "date": "2026-04-22",
  "kind": "event",
  "eventType": "birthday",
  "reason": "Geburtstag"
}
```

`reason` ist bei `kind=mass-import` Pflicht (max. 500 Zeichen), bei
`kind=event` optional. Fehlt `kind`, gilt `mass-import`.

#### Responses

| Status | Bedingung |
|--------|-----------|
| 201 Created | Markierung angelegt, Body = markierter Tag |
| 400 Bad Request | Ungueltiges Datum, `kind`, `eventType` oder fehlender Grund |
| 409 Conflict | Tag ist bereits markiert |

---

### 8. DELETE /api/stats/marked-days/{date}

**Zweck:** Markierung aufheben

**Auth:** `X-API-Key` erforderlich.

| Status | Bedingung |
|--------|-----------|
| 204 No Content | Markierung entfernt |
| 400 Bad Request | Datum nicht im Format `yyyy-MM-dd` |
| 404 Not Found | Tag ist nicht markiert |

---

### 9. POST /api/stats/snapshots/{id}/bean-hopper

**Zweck:** Bohnenfach eines Snapshot-Deltas manuell korrigieren

**Auth:** `X-API-Key` erforderlich.

#### Request

```http
POST /api/stats/snapshots/42/bean-hopper HTTP/1.1
Content-Type: application/json
X-API-Key: <key>

{
  "counter": "coffee",
  "beanHopper": 2
}
```

#### Path Parameters

| Parameter | Type | Beschreibung |
|-----------|------|--------------|
| id | int | Snapshot, an dem das Delta endet |

#### Body

| Feld | Type | Required | Beschreibung |
|------|------|----------|--------------|
| counter | string | Ja | `coffee` oder `coffeeAndMilk` |
| beanHopper | int \| null | Ja | `1`, `2` oder `null` fuer "kein Bohnenverbrauch". Muss im Body stehen — ein fehlendes Feld wird abgelehnt, nicht als `null` gelesen. |

Die Korrektur gilt fuer genau ein `(Snapshot, counter)`-Paar. Ein erneuter POST
auf dasselbe Paar ueberschreibt den Wert, es entsteht keine zweite Zeile.

#### Responses

| Status | Bedingung |
|--------|-----------|
| 204 No Content | Korrektur gespeichert |
| 400 Bad Request | `counter` unbekannt, `beanHopper` ausserhalb `1\|2\|null`, oder der Zaehler hat an diesem Snapshot kein Delta |
| 404 Not Found | Snapshot existiert nicht |

---

### 10. DELETE /api/stats/snapshots/{id}/bean-hopper

**Zweck:** Korrektur zuruecknehmen, das Delta faellt auf die Standardregel zurueck

**Auth:** `X-API-Key` erforderlich.

#### Request

```http
DELETE /api/stats/snapshots/42/bean-hopper?counter=coffee HTTP/1.1
X-API-Key: <key>
```

#### Query Parameters

| Parameter | Type | Required | Beschreibung |
|-----------|------|----------|--------------|
| counter | string | Ja | `coffee` oder `coffeeAndMilk` |

#### Responses

| Status | Bedingung |
|--------|-----------|
| 204 No Content | Korrektur entfernt |
| 400 Bad Request | `counter` unbekannt |
| 404 Not Found | Fuer dieses `(Snapshot, counter)`-Paar gibt es keine Korrektur |

---

### 11. GET /coffee/status

**Zweck:** Live-Zustand der Maschine (Server-Cache 7 s)

#### Response (200 OK)

```json
{
  "status": "ok",
  "reachable": true,
  "powerState": "on",
  "operationState": "ready",
  "label": "Bereit",
  "lastUpdated": "2026-04-25T09:12:00Z"
}
```

Die API fragt dafuer den n8n-Webhook (`N8n:PowerWebhookUrl`). Ist er nicht
erreichbar oder antwortet er nicht binnen 5 s, bleibt der Status `200 OK` mit
`reachable: false` und einer `message` — ein Ausfall des Relays ist kein
Fehler des Dashboards.

---

### 12. POST /coffee/power

**Zweck:** Maschine ein-/ausschalten (Relay ueber n8n)

**Auth:** `X-API-Key` erforderlich.

#### Request

```json
{ "state": "on" }
```

| Status | Bedingung |
|--------|-----------|
| 200 OK | Schaltbefehl an n8n uebergeben |
| 400 Bad Request | `state` ist weder `on` noch `off` |
| 429 Too Many Requests | Mehr als 10 Aufrufe in einem Ein-Minuten-Fenster |
| 500 Internal Server Error | Webhook nicht konfiguriert oder nicht erreichbar |

Das Limit greift vor der API-Key-Pruefung, verbraucht also auch bei
abgelehnten Aufrufen ein Kontingent — der Aufruf reicht bis zur BSH-Cloud
durch, und genau davor schuetzt es.

Das 07:00–18:00-Fenster ist eine reine UI-Sperre im Dashboard
(`coffeeTimeLock.ts`); die API prueft es nicht.

---

### 13. GET /api/health

**Zweck:** Healthcheck für Monitoring

#### Response (200 OK)

```json
{
  "status": "healthy",
  "timestamp": "2025-01-25T10:15:00Z",
  "database": "connected",
  "lastSnapshot": "2025-01-25T10:00:00Z"
}
```

---

## Datentypen

### StatusItemDto

```typescript
interface StatusItemDto {
  key: string;      // Home Connect Status Key
  value: any;       // string | number | boolean
  unit?: string;    // Optional (z.B. "ml")
}
```

### Home Connect Keys (Relevant)

| Key | Typ | Beschreibung |
|-----|-----|--------------|
| `BSH.Common.Status.OperationState` | string | Ready, Brewing, Cleaning, etc. |
| `BSH.Common.Status.RemoteControlStartAllowed` | boolean | Fernsteuerung erlaubt |
| `BSH.Common.Status.LocalControlActive` | boolean | Lokale Bedienung aktiv |
| `BSH.Common.Status.InteriorIlluminationActive` | boolean | Innenbeleuchtung an |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee` | int | Kaffee-Zähler |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk` | int | Milchkaffee-Zähler |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk` | int | Milch-Zähler |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups` | int | Heißwasser-Tassen |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater` | int | Heißwasser in ml |

---

## Idempotenz-Regeln

### Regel 1: Counter-Vergleich

Ein neuer Snapshot wird **nur** gespeichert, wenn mindestens ein Getränke-Counter größer ist als im letzten Snapshot:

```
NEW.BeverageCounterCoffee > OLD.BeverageCounterCoffee
OR NEW.BeverageCounterCoffeeAndMilk > OLD.BeverageCounterCoffeeAndMilk
OR NEW.BeverageCounterMilk > OLD.BeverageCounterMilk
OR NEW.BeverageCounterHotWaterCups > OLD.BeverageCounterHotWaterCups
```

### Regel 2: Erster Snapshot

Wenn keine Snapshots existieren, wird der erste immer gespeichert.

### Regel 3: Status-Änderungen

Status-Änderungen (OperationState, etc.) **ohne** Counter-Erhöhung führen **nicht** zu einem neuen Snapshot.

**Begründung:** Vermeidung von "Null-Zyklen" - uns interessiert primär der Konsum, nicht der Maschinenstatus.

---

## Fehlerbehandlung

### Standard Error Response

```json
{
  "error": "Error Type",
  "message": "Human readable message",
  "details": ["Optional array of specific issues"],
  "traceId": "abc123"
}
```

### HTTP Status Codes

| Code | Verwendung |
|------|------------|
| 200 | Erfolgreiche GET-Anfrage oder Duplikat-Ingest |
| 201 | Neuer Snapshot erstellt |
| 400 | Ungültige Anfrage (Validation Error) |
| 404 | Ressource nicht gefunden |
| 500 | Interner Serverfehler |

---

## n8n Workflow Spezifikation

### Workflow-Ablauf

```
[Cron Trigger: */15 7-2 * * *]
         │
         ▼
[HTTP Request: GET Home Connect Status]
         │
         ▼
[HTTP Request: POST /api/ingest]
         │
         ▼
[IF Node: Check Response]
    │         │
    ▼         ▼
[Log OK]  [Log Error + Notification]
```

### Cron Expression

`*/15 7-2 * * *` = Alle 15 Minuten von 07:00 bis 02:59 Uhr

### Erwartete Frequenz

- **Pro Tag:** Max. 76 Requests (19 Stunden × 4 pro Stunde)
- **Pro Woche:** Max. 532 Requests
- **Pro Monat:** Max. 2280 Requests

---

## OpenAPI Schema (Auszug)

```yaml
openapi: 3.0.3
info:
  title: Coffee Analytics Hub API
  version: 1.0.0
  description: API für Kaffee-Statistiken der Siemens EQ900

servers:
  - url: http://localhost:5000
    description: Development

paths:
  /api/ingest:
    post:
      summary: Ingest EQ900 Snapshot
      operationId: ingestSnapshot
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/IngestPayload'
      responses:
        '201':
          description: Snapshot created
        '200':
          description: Duplicate skipped
        '400':
          description: Invalid payload

components:
  schemas:
    IngestPayload:
      type: object
      required:
        - data
      properties:
        data:
          $ref: '#/components/schemas/IngestData'

    IngestData:
      type: object
      required:
        - status
      properties:
        status:
          type: array
          items:
            $ref: '#/components/schemas/StatusItem'

    StatusItem:
      type: object
      required:
        - key
        - value
      properties:
        key:
          type: string
        value:
          oneOf:
            - type: string
            - type: number
            - type: boolean
        unit:
          type: string
```

---

## Versionierung

| Version | Datum | Änderungen |
|---------|-------|------------|
| 1.0.0 | 2025-01-25 | Initial Spec |
