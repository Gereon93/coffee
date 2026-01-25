# Architecture: Coffee Analytics Hub (EQ900)

## Tech Stack

| Layer | Technologie | Version | Begründung |
|-------|-------------|---------|------------|
| Backend | ASP.NET Core Web API | .NET 10 | LTS, Performance, Modern Features |
| API-Dokumentation | Scalar | Latest | Moderner Swagger-Ersatz, bessere UX |
| Datenbank | SQLite | 3.x | Lokal, robust, einfach zu sichern |
| ORM | Entity Framework Core | 9.x | Code-First, Migrations |
| Data Fetcher | n8n (extern) | - | OAuth2-Handling, Scheduling |
| Frontend | React + Vite | 18.x | SPA, Charts, moderne DX |

---

## System-Kontext (C4 Level 1)

```
┌─────────────────┐     OAuth2      ┌─────────────────────┐
│  Home Connect   │◄───────────────►│        n8n          │
│     Cloud       │                 │   (Data Fetcher)    │
└─────────────────┘                 └──────────┬──────────┘
                                               │
                                    HTTP POST /api/ingest
                                               │
                                               ▼
                                    ┌──────────────────────┐
                                    │   Coffee Analytics   │
                                    │    Hub (.NET 10)     │
                                    │                      │
                                    │  ┌────────────────┐  │
                                    │  │  SQLite DB     │  │
                                    │  └────────────────┘  │
                                    └──────────┬──────────┘
                                               │
                                      REST API (GET)
                                               │
                                               ▼
                                    ┌──────────────────────┐
                                    │   React Dashboard    │
                                    └──────────────────────┘
```

---

## Container-Diagramm (C4 Level 2)

### A. n8n Workflow (Extern)
- **Verantwortung:** OAuth2-Token-Management, Scheduling, Fehlerbehandlung
- **Trigger:** Cron `*/15 7-2 * * *` (alle 15 Min, 07:00-02:00)
- **Output:** POST JSON an `/api/ingest`

### B. Coffee Analytics Hub (.NET 10 Web API)
- **Projekt:** `CoffeeApi`
- **Framework:** ASP.NET Core Minimal APIs oder Controller-basiert
- **Packages:**
  - `Microsoft.EntityFrameworkCore.Sqlite`
  - `Scalar.AspNetCore`
- **Verantwortung:**
  - Daten von n8n empfangen und validieren
  - Idempotenz-Check durchführen
  - Persistenz in SQLite
  - Read-API für Frontend bereitstellen

### C. React Dashboard
- **Projekt:** `CoffeeWeb` (Neubau mit Vite)
- **Verantwortung:** Visualisierung, Heatmaps, Tagesvergleiche
- **API-Client:** Generiert via Scalar/OpenAPI

---

## API Endpoints

### Ingest (Write)

| Method | Endpoint | Beschreibung |
|--------|----------|--------------|
| POST | `/api/ingest` | n8n liefert EQ900-Snapshot |

### Statistics (Read)

| Method | Endpoint | Beschreibung |
|--------|----------|--------------|
| GET | `/api/stats` | Alle Snapshots (paginiert) |
| GET | `/api/stats/daily/{date}` | Tagesstatistik |
| GET | `/api/stats/heatmap` | Aggregierte Heatmap-Daten |
| GET | `/api/stats/range?from=&to=` | Zeitraum-Abfrage |

---

## Datenfluss (Sequence)

```
n8n                      CoffeeApi                    SQLite
 │                           │                           │
 │  POST /api/ingest (JSON)  │                           │
 │──────────────────────────►│                           │
 │                           │  Load last snapshot       │
 │                           │──────────────────────────►│
 │                           │◄──────────────────────────│
 │                           │                           │
 │                           │  Compare counters         │
 │                           │  (Idempotenz-Check)       │
 │                           │                           │
 │                           │  IF new > old: INSERT     │
 │                           │──────────────────────────►│
 │                           │                           │
 │      201 Created / 200 OK │                           │
 │◄──────────────────────────│                           │
```

---

## Projekt-Struktur (Ziel)

```
/CoffeeApi
├── Controllers/
│   ├── IngestController.cs      # POST /api/ingest
│   └── StatsController.cs       # GET /api/stats/*
├── Domain/
│   └── MachineSnapshot.cs       # Entity
├── DTOs/
│   ├── IngestPayloadDto.cs      # n8n Input
│   └── SnapshotResponseDto.cs   # API Output
├── Infrastructure/
│   ├── AppDbContext.cs          # EF Core Context
│   └── Migrations/
├── Services/
│   └── SnapshotService.cs       # Idempotenz-Logik
├── Program.cs                   # Startup, DI, Scalar
└── appsettings.json
```

---

## Konfiguration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "Default": "Data Source=coffee.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Environment Variables (Production)
- `ConnectionStrings__Default`: SQLite-Pfad
- `ASPNETCORE_ENVIRONMENT`: Production

---

## Scalar Integration

```csharp
// Program.cs
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(); // /scalar/v1

app.MapControllers();
```

---

## Migration von Swagger zu Scalar

| Alt (Swagger) | Neu (Scalar) |
|---------------|--------------|
| `Swashbuckle.AspNetCore` | `Scalar.AspNetCore` |
| `/swagger` | `/scalar/v1` |
| `SwaggerGen` | `AddOpenApi()` |

---

## Abhängigkeiten (NuGet Packages)

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
  <PackageReference Include="Scalar.AspNetCore" Version="2.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />
</ItemGroup>
```

---

## Nicht-funktionale Anforderungen

| Anforderung | Ziel |
|-------------|------|
| Response Time | < 100ms für Reads |
| Verfügbarkeit | 99% (Self-Hosted) |
| Datenhaltung | Lokal, kein Cloud-Lock-in |
| Backup | SQLite-File kopieren |

---

## Entscheidungslog

| Datum | Entscheidung | Begründung |
|-------|--------------|------------|
| 2025-01 | SQLite statt MongoDB | Einfacher, kein Server nötig |
| 2025-01 | n8n statt eigener Worker | OAuth2-Komplexität auslagern |
| 2025-01 | Scalar statt Swagger | Modernere UI, bessere DX |
| 2025-01 | .NET 10 | LTS, aktuelle Features |
