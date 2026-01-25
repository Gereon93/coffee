# Design: Coffee Analytics Hub (EQ900)

## Domain Model

### MachineSnapshot (Core Entity)

```csharp
public class MachineSnapshot
{
    public int Id { get; set; }

    // Zeitstempel des Snapshots (von n8n geliefert)
    public DateTime Timestamp { get; set; }

    // Maschinen-Identifikation
    public string MachineId { get; set; } = "EQ900-DEFAULT";

    // Getränke-Zähler
    public int BeverageCounterCoffee { get; set; }
    public int BeverageCounterCoffeeAndMilk { get; set; }
    public int BeverageCounterMilk { get; set; }
    public int BeverageCounterHotWaterCups { get; set; }
    public int BeverageCounterHotWater { get; set; } // in ml

    // Status
    public string OperationState { get; set; } = "Ready";
    public bool RemoteControlAllowed { get; set; }
    public bool LocalControlActive { get; set; }
    public bool InteriorIlluminationActive { get; set; }

    // Metadaten
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## SQLite Schema

### Tabelle: MachineSnapshots

```sql
CREATE TABLE MachineSnapshots (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL,
    MachineId TEXT NOT NULL DEFAULT 'EQ900-DEFAULT',

    -- Beverage Counters
    BeverageCounterCoffee INTEGER NOT NULL,
    BeverageCounterCoffeeAndMilk INTEGER NOT NULL,
    BeverageCounterMilk INTEGER NOT NULL,
    BeverageCounterHotWaterCups INTEGER NOT NULL,
    BeverageCounterHotWater INTEGER NOT NULL,

    -- Status Fields
    OperationState TEXT NOT NULL,
    RemoteControlAllowed INTEGER NOT NULL,
    LocalControlActive INTEGER NOT NULL,
    InteriorIlluminationActive INTEGER NOT NULL,

    -- Metadata
    CreatedAt TEXT NOT NULL
);

-- Indizes für Performance
CREATE INDEX IX_MachineSnapshots_Timestamp ON MachineSnapshots(Timestamp);
CREATE INDEX IX_MachineSnapshots_MachineId ON MachineSnapshots(MachineId);
CREATE UNIQUE INDEX IX_MachineSnapshots_Idempotency
    ON MachineSnapshots(MachineId, BeverageCounterCoffee, BeverageCounterCoffeeAndMilk, BeverageCounterMilk);
```

---

## DTOs

### IngestPayloadDto (Input von n8n)

```csharp
public class IngestPayloadDto
{
    public IngestDataDto Data { get; set; } = new();
}

public class IngestDataDto
{
    public List<StatusItemDto> Status { get; set; } = new();
}

public class StatusItemDto
{
    public string Key { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
    public string? Unit { get; set; }
}
```

### Beispiel n8n Payload

```json
{
  "data": {
    "status": [
      { "key": "BSH.Common.Status.OperationState", "value": "BSH.Common.EnumType.OperationState.Ready" },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "value": 988 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", "value": 10 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", "value": 11 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", "value": 1 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater", "value": 150, "unit": "ml" }
    ]
  }
}
```

### SnapshotResponseDto (Output)

```csharp
public class SnapshotResponseDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int TotalBeverages => BeverageCounterCoffee + BeverageCounterCoffeeAndMilk + BeverageCounterMilk;
    public int BeverageCounterCoffee { get; set; }
    public int BeverageCounterCoffeeAndMilk { get; set; }
    public int BeverageCounterMilk { get; set; }
    public string OperationState { get; set; } = string.Empty;
}
```

---

## Service Layer

### ISnapshotService Interface

```csharp
public interface ISnapshotService
{
    Task<(bool Created, MachineSnapshot Snapshot)> ProcessIngestAsync(IngestPayloadDto payload);
    Task<MachineSnapshot?> GetLatestAsync(string machineId);
    Task<IEnumerable<MachineSnapshot>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task<DailyStatsDto> GetDailyStatsAsync(DateOnly date);
}
```

### Idempotenz-Logik (SnapshotService)

```csharp
public async Task<(bool Created, MachineSnapshot Snapshot)> ProcessIngestAsync(IngestPayloadDto payload)
{
    var newSnapshot = MapToEntity(payload);

    // 1. Letzten Snapshot laden
    var lastSnapshot = await GetLatestAsync(newSnapshot.MachineId);

    // 2. Idempotenz-Check: Nur speichern wenn sich Zähler erhöht haben
    if (lastSnapshot != null && !HasCounterIncreased(lastSnapshot, newSnapshot))
    {
        _logger.LogDebug("Snapshot skipped - no counter increase");
        return (false, lastSnapshot);
    }

    // 3. Neuen Snapshot speichern
    _context.MachineSnapshots.Add(newSnapshot);
    await _context.SaveChangesAsync();

    _logger.LogInformation("New snapshot created: {Id}", newSnapshot.Id);
    return (true, newSnapshot);
}

private bool HasCounterIncreased(MachineSnapshot last, MachineSnapshot current)
{
    return current.BeverageCounterCoffee > last.BeverageCounterCoffee
        || current.BeverageCounterCoffeeAndMilk > last.BeverageCounterCoffeeAndMilk
        || current.BeverageCounterMilk > last.BeverageCounterMilk
        || current.BeverageCounterHotWaterCups > last.BeverageCounterHotWaterCups;
}
```

---

## Key Mapping (Home Connect -> Domain)

| Home Connect Key | Domain Property |
|------------------|-----------------|
| `BSH.Common.Status.OperationState` | OperationState |
| `BSH.Common.Status.RemoteControlStartAllowed` | RemoteControlAllowed |
| `BSH.Common.Status.LocalControlActive` | LocalControlActive |
| `BSH.Common.Status.InteriorIlluminationActive` | InteriorIlluminationActive |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee` | BeverageCounterCoffee |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk` | BeverageCounterCoffeeAndMilk |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk` | BeverageCounterMilk |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups` | BeverageCounterHotWaterCups |
| `ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater` | BeverageCounterHotWater |

---

## User Flows

### Flow 1: Daten-Ingest (n8n -> API)

```
1. n8n Cron triggert (alle 15 Min)
2. n8n holt Status von Home Connect API
3. n8n transformiert Payload
4. n8n POST an /api/ingest
5. API validiert JSON
6. API prüft Idempotenz (Counter-Vergleich)
7. API speichert oder verwirft
8. API antwortet 201 (created) oder 200 (duplicate)
```

### Flow 2: Dashboard-Ansicht (User)

```
1. User öffnet React App
2. App lädt /api/stats/daily/{today}
3. App rendert Tages-Chart
4. User wählt Zeitraum
5. App lädt /api/stats/range?from=&to=
6. App aktualisiert Visualisierung
```

---

## UI Komponenten (React)

### Geplante Views

| View | Route | Beschreibung |
|------|-------|--------------|
| Dashboard | `/` | Tages-Übersicht, aktuelle Zähler |
| History | `/history` | Kalender-Ansicht, Zeitraum-Filter |
| Heatmap | `/heatmap` | Stunden x Wochentag Matrix |
| Settings | `/settings` | API-URL, Theme |

### Chart-Bibliothek

- **Empfehlung:** Recharts oder Chart.js
- **Grund:** React-native, gute Performance, einfache API

---

## Design-Prinzipien

1. **Mobile First:** Dashboard muss auf Smartphone nutzbar sein
2. **Dark Mode:** Optional, System-Präferenz respektieren
3. **Minimal UI:** Fokus auf Daten, keine Ablenkung
4. **Offline-Tolerant:** Graceful Degradation bei API-Ausfall

---

## Validation Rules

### Ingest Payload

| Feld | Regel |
|------|-------|
| `data` | Required, nicht null |
| `data.status` | Required, Array mit >= 1 Element |
| `status[].key` | Required, nicht leer |
| `status[].value` | Required |

### Business Rules

| Regel | Beschreibung |
|-------|--------------|
| Idempotenz | Nur speichern wenn min. 1 Counter > letzter Wert |
| Zeitfenster | Daten nur von 07:00 - 02:00 erwartet |
| Counter-Monotonie | Counter dürfen nie sinken (außer Reset) |

---

## Error Handling

### API Response Codes

| Code | Bedeutung |
|------|-----------|
| 200 OK | Payload verarbeitet, aber kein neuer Snapshot (Duplikat) |
| 201 Created | Neuer Snapshot gespeichert |
| 400 Bad Request | Ungültiges JSON oder fehlende Felder |
| 500 Internal Server Error | DB-Fehler oder unerwarteter Zustand |

### Logging-Format

```json
{
  "timestamp": "2025-01-25T10:15:00Z",
  "level": "Information",
  "message": "Snapshot processed",
  "properties": {
    "snapshotId": 42,
    "created": true,
    "coffeeCount": 988
  }
}
```
