# Soll-Stand (Zielbild)

## Zielueberblick
- Fokus auf Siemens EQ900 statt Nivona (Begriffe und Datenmodell entsprechend umbenennen).
- Datenabruf ueber die Siemens EQ900 API (Statistiken direkt vom Geraet/Cloud).
- Persistenz der Statistiken in MongoDB, inklusive Idempotenz.
- Ziel-Framework: .NET 10 fuer Backend/Worker.
- CoffeeWeb wird Richtung React.js migriert (neue Web-UI).

## Zentrale Ziele (Prioritaet)
1) API anbieten für n8n welches die daten von der eq900 liefert
2) Persistenz in MongoDB mit sauberem Schema und Duplikat-Schutz.
3) Scheduler fuer stundenweisen Abruf.
4) Migration auf .NET 10.
5) CoffeeWeb-UI als React.js App (statt Blazor Server).

## Architektur (Soll)
[api welches daten von n8n erwartet]
          �
          ?
[ MongoDB ]
          �
          ?
[ CoffeeWeb React UI ]  (optional: liest ueber REST API)

Optional:
[ REST API (.NET 10 Web API) ] fuer UI/Reporting

## Komponenten (Soll)
### EQ900 Agent Service
- .NET 10 Worker/BackgroundService.
- OAuth2-Flow (falls erforderlich) inkl. Refresh Token.
- Stundlicher Scheduler (z. B. Quartz.NET oder Hangfire) oder Cron.
- Logging und Fehlerbehandlung.
- Optional: Healthcheck/Metriken.

### Persistenz (MongoDB)
- Neue Collection z. B. `eq900_statistics`.
- Idempotenz ueber eindeutige Keys (z. B. `machine_id + timestamp` oder API-Event-ID).
- Indizes fuer Zeit und Geraet.

Beispiel-Dokument:
{
  "_id": "ObjectId",
  "timestamp": "2025-10-23T10:00:00Z",
  "machine_id": "EQ900-12345",
  "data": {
    "coffeeCount": 1245,
    "cleaningCycles": 12,
    "lastDrinkType": "Espresso"
  },
  "source": "siemens-eq900-api"
}

### REST API (optional/empfohlen)
- .NET 10 Web API fuer UI-Zugriff und Admin-Operationen.
- Endpunkte fuer Statistiken, Filterung, Zeitraeume.

### CoffeeWeb (React.js)
- Neue React-App (z. B. Vite oder Next.js) mit Charts.
- Holt Daten ueber REST API.
- Ablosung der bestehenden Blazor-Seiten.

## Migration/Umbenennungen
- Alle "Nivona"-Begriffe/Dateien auf EQ900 anpassen.
- Collections, Modelle, Controller, UI-Routen entsprechend neu benennen.

## Offene Punkte
- Details zur EQ900 API: Auth-Flow, Endpunkte, Rate Limits, Datenformat.
- Wo sollen Tokens sicher gespeichert werden (z. B. MongoDB, Secret Store)?
- Wahl des Schedulers (Quartz.NET/Hangfire/cron).
- Ziel: UI nur lesen oder auch Aktionen?
