# 🔭 Vision: Coffee Analytics Hub (EQ900)

## 1. Kernziel
Aufbau einer hochfrequenten Datenerfassungs-Pipeline für die Siemens EQ900, um Konsummuster präzise zu analysieren.

- **Fokus:** Ausschließlich Kaffee-Statistiken (Bezüge, Reinigung, Status).
- **Dashboard-Ziel:** Visualisierung der "Kaffee-Peaks" über den Tag, Wochentagsvergleiche und Heatmaps.
- **Kontext:** Siehe `IST_STAND.md` für die aktuelle Ausgangslage.

## 2. Die "15-Minuten-Dump"-Strategie
Um eine granulare Auswertung zu ermöglichen, werden die Daten in einem engen Zeitfenster erfasst:

- **Intervall:** Alle 15 Minuten (4-mal pro Stunde).
- **Zeitraum:** 07:00 Uhr bis 02:00 Uhr (Abdeckung des gesamten Wach-Rhythmus).
- **Idempotenz-Logik (Backend):**
  - Die .NET API prüft bei jedem Dump, ob sich die Zählerstände seit dem letzten Eintrag erhöht haben.
  - Nur bei einer Differenz (oder einer Statusänderung) wird ein neuer permanenter Datensatz in **SQLite** angelegt.
  - *Ziel:* Vermeidung von redundanten "Null-Zyklen" in der Datenbank.

## 3. Architektur & Komponenten

### A. Data Fetcher (n8n)
- **Funktion:** Agiert als Scheduler und API-Gateway zur BSH Home Connect Cloud.
- **Handling:** Übernimmt den OAuth2-Token-Refresh und die Fehlerbehandlung (z.B. Maschine offline).
- **Payload:** Sendet einen bereinigten JSON-Dump an die .NET API.

### B. Core Hub (.NET 10 Web API)
- **Framework:** .NET 10 (Migration des alten `CoffeeApi` Projekts).
- **Dokumentation:** Scalar (Moderner Ersatz für Swagger).
- **Datenhaltung:** **SQLite** (lokal, robust, einfach zu sichern).

### C. Frontend (React Dashboard)
- **Technologie:** Migration von Blazor zu React (Vite).
- **Features:** Explorer-Ansicht für historische Daten, Tages-Vergleiche und Trend-Analysen.

## 4. Datenmodell (Draft)
Ein typischer "Dump"-Eintrag in der Datenbank umfasst:
- `Timestamp` (ISO-8601)
- `BeverageCounterTotal` (Summe aller Bezüge)
- `BeverageCounterMilk` (Spezifisch Milchmischgetränke)
- `MachineState` (Ready, Brewing, Cleaning, etc.)

### Beispiel Payload (Home Connect)
```json
{
  "data": {
    "status": [
      { "key": "BSH.Common.Status.InteriorIlluminationActive", "value": false },
      { "key": "BSH.Common.Status.OperationState", "value": "BSH.Common.EnumType.OperationState.Ready" },
      { "key": "BSH.Common.Status.RemoteControlStartAllowed", "value": true },
      { "key": "BSH.Common.Status.LocalControlActive", "value": false },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "value": 988 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", "value": 10 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", "value": 11 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", "value": 1 },
      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater", "value": 150, "unit": "ml" }
    ]
  }
}
```

## 5. Integration in Agent-Workflow
Diese Vision definiert die Aufgaben für die Agents:

- **`ARCHITECTURE.md`:** Muss den `/api/ingest`-Endpunkt, .NET 10 Update und Scalar.AspNetCore Integration definieren.
- **`DESIGN.md`:** Beschreibt das SQLite-Schema und die Idempotenz-Logik (Service-Layer).
- **`PROJECT_STATE.md`:** Dokumentiert den Pivot von Nivona/MongoDB zu EQ900/SQLite.
- **`SPEC.md`:** Definiert die API-Kontrakte zwischen n8n und .NET.
