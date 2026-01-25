# API Specification: Coffee Analytics Hub

## Übersicht

Diese Spezifikation definiert die API-Kontrakte zwischen:
- **n8n** (Data Fetcher) → **CoffeeApi** (Ingest)
- **React Frontend** → **CoffeeApi** (Read)

---

## Base URL

| Environment | URL |
|-------------|-----|
| Development | `http://localhost:5000` |
| Production | `https://coffee.local` (TBD) |

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
      "operationState": "Ready"
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

#### Response (200 OK)

```json
{
  "date": "2025-01-25",
  "snapshots": [
    {
      "timestamp": "2025-01-25T07:00:00Z",
      "beverageCounterCoffee": 980,
      "totalBeverages": 1000
    },
    {
      "timestamp": "2025-01-25T07:15:00Z",
      "beverageCounterCoffee": 982,
      "totalBeverages": 1002
    }
  ],
  "summary": {
    "coffeeToday": 8,
    "milkDrinksToday": 2,
    "totalToday": 10,
    "peakHour": 9
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
      "total": 50
    },
    {
      "date": "2025-01-21",
      "coffeeCount": 42,
      "milkCount": 8,
      "total": 50
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
| weeks | int | 4 | Anzahl Wochen zurück |

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

### 6. GET /api/health

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
