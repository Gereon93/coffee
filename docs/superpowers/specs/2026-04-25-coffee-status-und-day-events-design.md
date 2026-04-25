# Coffee Status, Power-Toggle und Day-Event-Annotationen

**Datum:** 2026-04-25
**Scope:** `CoffeeApi` + `coffee-dashboard` (dashboard-s7 separat, durch User selbst angebunden)

---

## Ziel

Drei zusammenhängende Erweiterungen für das bestehende Coffee-Analytics-System:

1. **Live-Status der Kaffeemaschine** über `GET /coffee/status` — bisher ist der Maschinenzustand nur indirekt aus Snapshots ersichtlich (alle 15 Min) und im Briefing als Hauptauftrag definiert.
2. **Power-Toggle im coffee-dashboard** — Button in der NavBar (analog zum Theme-Toggle), der den aktuellen Status zeigt und sowohl Ein- als auch Ausschalten erlaubt.
3. **Tages-Annotationen für Events** — markierte Tage (Geburtstag, Besuch, Feier etc.) erklären Ausreißer in der Statistik, ohne sie aus der Aggregation zu entfernen. Abgrenzung zum bestehenden Massenimport-Flag, das Tage *aus den Stats ausblendet*.

Die Mass-Import-Funktion bleibt erhalten, wird aber unter ein vereinheitlichtes Datenmodell gezogen.

---

## 1. Datenmodell

### Migration: `ExcludedDay` → `MarkedDay`

```
MarkedDay
├── Date         DateOnly  (PK, yyyy-MM-dd)
├── Kind         string    ("mass-import" | "event")
├── EventType    string?   (nur wenn Kind="event": "birthday"|"visitors"|"party"|"sick"|"vacation"|"other")
├── Reason       string    (Freitext-Notiz; bei mass-import ist das die alte "Reason"-Bedeutung)
└── CreatedAt    DateTime
```

**EF-Core-Migration:** `RenameExcludedDaysToMarkedDays`

- Tabelle `ExcludedDays` → `MarkedDays` umbenennen.
- Spalte `Kind` (NOT NULL, Default `"mass-import"`) hinzufügen — alle existierenden Rows werden so automatisch korrekt klassifiziert.
- Spalte `EventType` (nullable) hinzufügen.
- Auto-Migrate beim Container-Start ist aktiv (Auto-Baseliner aus Phase 12), kein manueller DB-Eingriff.

**Eindeutigkeit:** ein `MarkedDay` pro `Date`. Wer einen Event-Tag in einen Massenimport-Tag (oder umgekehrt) umwandeln will → erst löschen, dann neu markieren.

**Validierungsregel:** wenn `Kind="event"` → `EventType` ist required. Wenn `Kind="mass-import"` → `EventType` ist null.

---

## 2. API-Endpoints

### 2.1 Day-Markierungen

Bestehender `ExcludedDaysController` wird zu `MarkedDaysController` (neue Route `/api/stats/marked-days`).

```
GET    /api/stats/marked-days[?kind=event|mass-import]
POST   /api/stats/marked-days        body: { date, kind, eventType?, reason }
DELETE /api/stats/marked-days/{date}
```

**Response-DTO `MarkedDayDto`:**
```json
{
  "date":      "2026-04-22",
  "kind":      "event",
  "eventType": "birthday",
  "reason":    "Schwiegereltern da",
  "createdAt": "2026-04-25T10:30:00Z"
}
```

**Verhalten:**
- `GET` ohne Filter → alles. `?kind=event` → nur Events. `?kind=mass-import` → nur Massenimport.
- `POST` 400 bei: ungültigem `date` (`yyyy-MM-dd`), ungültigem `kind`, `kind=event` ohne `eventType`, leerer `reason` *außer* bei `kind=event` (da ist Reason optional, kann auch nur Quick-Pick sein).
- `POST` 409 wenn Date bereits markiert (egal welche Kind).
- `DELETE` 404 wenn Date nicht markiert.

### 2.2 Stats-Aggregationen — Filter-Update

Endpoints `/api/stats/range`, `/api/stats/heatmap`, `/api/stats/daily/{date}` filtern nur noch `Kind="mass-import"` raus. `Kind="event"` Tage bleiben in der Aggregation drin (ihre Daten sind valide, nur erklärt).

Anomalie-Erkennung im Frontend (`useAnomalyDetection`) entfernt **beide** Kinds aus Baseline und Marker-Liste — Event-Tage sind erklärte Ausreißer und sollen nicht erneut alarmieren.

### 2.3 Coffee-Status — neu

```
GET /coffee/status
```

**Response-Schema (Erfolg, `200 OK`):**
```json
{
  "status":         "ok",
  "reachable":      true,
  "powerState":     "on",
  "operationState": "ready",
  "label":          "Bereit",
  "lastUpdated":    "2026-04-25T08:42:11Z"
}
```

**Response-Schema (n8n nicht erreichbar, `200 OK` mit `reachable=false`):**
```json
{
  "status":         "ok",
  "reachable":      false,
  "powerState":     null,
  "operationState": null,
  "label":          "Offline",
  "lastUpdated":    "2026-04-25T08:30:00Z",
  "message":        "Status-Service nicht erreichbar"
}
```

**Echter API-Fehler (`500`):**
```json
{ "status": "error", "message": "..." }
```

**Implementation:**
- API ruft den **bestehenden** n8n-Power-Webhook auf, aber per `GET` (existing PUT bleibt für Power-Schalten). Der n8n-Workflow wird um einen GET-Branch erweitert, der HomeConnect abfragt und das Schema oben zurückgibt.
- Keine neue Config-Property nötig — `N8n:PowerWebhookUrl` wird wiederverwendet, Basic-Auth-Credentials ebenso.
- IMemoryCache: 7s TTL für die Status-Response, schont BSH-Quota bei Hammer-Pollings.
- Bei n8n-Timeout (>5s) oder non-2xx → API antwortet `200` mit `reachable:false` und passender `message`. Kein 5xx in diesem Fall — der API-Service selbst läuft ja.
- 5xx nur bei echten internen Fehlern (Code-Bug, IMemoryCache-Probleme).

### 2.4 Power-Endpoint (unverändert)

`POST /coffee/power` mit `{ "state": "on" | "off" }` bleibt wie er ist.

---

## 3. Backend-Service-Änderungen

### `IHomeConnectService` erweitert

```csharp
public interface IHomeConnectService
{
    Task SetPowerStateAsync(bool on);            // existing
    Task<CoffeeStatusDto> GetStatusAsync();      // new
}
```

`HomeConnectService.GetStatusAsync()`:
- `_httpClient.GetAsync(_webhookUrl)` (Basic-Auth-Header steht schon am Client).
- Timeout 5s.
- Response deserialisieren auf `CoffeeStatusDto`.
- Bei Exception oder non-2xx → `CoffeeStatusDto { reachable=false, message=... }` zurückgeben (nicht throwen).

### Neuer Controller `CoffeeStatusController`

```
[Route("coffee")]
GET /status   →   _cache.GetOrCreate("coffee:status", 7s, () => _homeConnect.GetStatusAsync())
```

### Stats-Filter

`SnapshotService` (alle Methoden, die `ExcludedDays` referenzieren) filtert auf `Kind == "mass-import"`. Test-Suite wird entsprechend angepasst.

---

## 4. Frontend: Power-Status in der NavBar

### Neue Komponente `CoffeePowerButton`

In `coffee-dashboard/src/components/layout/CoffeePowerButton.tsx`, eingebettet in `NavBar.tsx` rechts neben dem Theme-Toggle. Style mirror der Theme-Toggle-Pille (`rounded-full`, `border`, gleiche Größe), Icon `Coffee` aus `lucide-react`.

### Hook `useCoffeeStatus`

- TanStack Query, Query-Key `["coffee", "status"]`.
- `staleTime: Infinity`, `refetchOnWindowFocus: false`, `refetchOnMount: true` → echtes On-Demand-Verhalten: einmal beim Mount der NavBar geladen, danach nur nach manueller Invalidation.
- API-URL aus existing `api/client.ts` (gleicher Base wie andere Endpoints).

### Hook `useSetCoffeePower`

- TanStack Mutation, POST `/coffee/power`.
- onSuccess: `setTimeout(() => queryClient.invalidateQueries({ queryKey: ["coffee", "status"] }), 3000)` — 3s Delay, damit BSH Zeit hat zu reagieren.
- onError: Toast mit Fehler-Message.

### Render-States

| Zustand | Label | Klick sendet | Farbe | Klickbar |
|---|---|---|---|---|
| `reachable=false` | „Offline" | – | grau | nein |
| Zeitsperre 18–07h (lokal Berlin) | „Gesperrt" | – | grau | nein |
| `powerState ∈ {off, standby}` | „Einschalten" | `state:"on"` | stone | ja |
| `powerState=on` + `operationState=ready` | „Ausschalten" | `state:"off"` | emerald | ja |
| `powerState=on` + `operationState=run` | „Läuft" | – | sky | nein |
| Mutation-Pending oder 3s nach Erfolg (bis Refetch) | „Schaltet…" | – | amber | nein |

Klick-Logik: `nextState = currentPowerState === "on" ? "off" : "on"`.

Da wir nach jedem Klick den Status neu holen (3s Delay + Refetch), brauchen wir keinen fixen 10s-Cooldown wie dashboard-s7. Sobald der Refetch den neuen Zustand liefert, kippt das Label automatisch.

### Zeitsperre

`coffeeAllowed()` Helper, prüft Europe/Berlin-Zeit, `false` für `hour >= 18 || hour < 7`. Blockiert beide Richtungen — BSH-Auto-Standby übernimmt nachts.

### Toast-Notifications

Wird im Implementation-Plan abgeklärt: bestehender Mechanismus oder minimaler eigener `useToast`-Hook (oder `sonner` falls schon dep). Out-of-scope für diese Spec.

---

## 5. Frontend: Event-Annotationen im Dashboard

### Klick-Interaktion auf `DailyBarChart`

Recharts-Bars bekommen `onClick` — Klick öffnet `MarkDayEventModal` mit dem Datum vorausgefüllt.

### Neue Komponente `MarkDayEventModal`

Pfad `coffee-dashboard/src/components/dashboard/MarkDayEventModal.tsx`.

**UI-Aufbau:**
- Header: „Tag markieren — Mi 22.04.2026" (formatiertes deutsches Datum).
- 6 Quick-Pick-Buttons (Emoji + Label):
  - 🎂 Geburtstag (`birthday`)
  - 👥 Besuch (`visitors`)
  - 🎉 Feier (`party`)
  - 🏥 Krank (`sick`)
  - ✈️ Urlaub (`vacation`)
  - 📌 Sonstiges (`other`)
- Freitext-Feld „Notiz (optional)".
- Save-Button → POST `/api/stats/marked-days` mit `kind=event`.

**Drei Modi:**
1. **Tag unmarkiert** → Quick-Picks + Freitext, Save = neu anlegen.
2. **Tag hat Event-Annotation** → aktuelle Auswahl + Notiz vorausgefüllt; „Speichern" (= alte löschen + neu anlegen, da PUT nicht im Scope) und „Entfernen"-Button.
3. **Tag ist Massenimport** → nur Hinweis „Tag ist als Massenimport markiert (entfernen über Log-Seite)". Kein Override aus dem Dashboard.

### Visuelle Marker im DailyBarChart

- Bars mit Event-Annotation: Emoji-Badge oben am Balken (Recharts Custom Layer / `<text>`).
- Bars mit Massenimport: bleiben ausgegraut wie aktuell.
- Custom-Tooltip: zusätzlich zur Verbrauchszahl die Event-Notiz, z.B. „🎂 Geburtstag — Schwiegereltern da".

### Hooks-Refactor

- `useExcludedDays()` → wird zu `useMarkedDays()`. Gibt alle Marker zurück + Helper:
  - `getByDate(dateKey)` → liefert `MarkedDay | undefined`
  - `excludedSet` (Set von Dates mit `kind=mass-import`) für bestehende LogPage-Logik
- LogPage benutzt weiterhin `excludedSet` — minimal-invasive Änderung.
- `useExcludedDays()` als dünner Backward-Compat-Wrapper bleibt für LogPage und filtert auf `kind=mass-import`. (Optional: später inline ziehen.)
- Neuer Hook `useAddMarkedDay` (POST), `useRemoveMarkedDay` (DELETE) — ersetzt die zwei bestehenden ExcludedDay-Mutations.

### Anomalie-Detection (`useAnomalyDetection`)

Vor der Z-Score-Berechnung werden **alle** markierten Tage (egal Kind) aus der Tagesliste entfernt. Das hat zwei Effekte:
1. Sie fließen nicht in die Baseline (Mittelwert, Std-Abweichung) ein → keine verzerrte Statistik.
2. Sie werden nicht als Anomalie markiert → keine doppelte Information („Anomalie + erklärt").

---

## 6. Konfiguration & Deployment

### appsettings.json (unverändert)

```json
"N8n": {
  "PowerWebhookUrl":   "https://n8n.murg-api.work/webhook/coffee-power",
  "BasicAuthUser":     "<existing>",
  "BasicAuthPassword": "<existing>"
}
```

Keine neue Property. `PowerWebhookUrl` wird sowohl für PUT (Power-Schalten) als auch für GET (Status-Lesen) genutzt.

### n8n-Workflow (außerhalb dieses Repos)

Bestehender `coffee-power`-Workflow wird erweitert:
- Webhook-Trigger akzeptiert zusätzlich GET.
- GET-Branch holt `GET https://api.home-connect.com/api/homeappliances/{haId}` (OAuth-Token aus n8n-Credentials).
- Mapped die BSH-Antwort auf das Briefing-Schema (powerState, operationState normalisieren, deutsches `label` vorberechnen, `lastUpdated` setzen).
- Bei BSH-Fehler/Timeout: `200` mit `{ status: "ok", reachable: false, message: "..." }`.

### Build/Deploy

Keine Änderung am `build.sh`-Flow oder an der Compose-Konfiguration. EF-Migration läuft beim Container-Start automatisch.

---

## 7. Tests

### Backend (CoffeeTest, EF Core InMemory)

| Testklasse | Cases |
|---|---|
| `MarkedDayMigrationTests` (neu) | Existing `ExcludedDay`-Rows kriegen `kind=mass-import` nach Migration; Tabelle ist umbenannt. |
| `MarkedDaysControllerTests` (neu) | GET ohne Filter, GET ?kind=event, GET ?kind=mass-import; POST `kind=event` + `eventType` ok; POST `kind=mass-import` ok (backward-compat); 400 bei ungültigem `kind`, fehlendem `eventType` wenn `kind=event`, ungültigem Datum; 409 bei Doppel-Date; DELETE 204 / 404. |
| `SnapshotServiceQueryTests` (erweitern) | Range/Heatmap-Aggregation excluded nur `kind=mass-import`; `kind=event`-Tage bleiben drin. |
| `CoffeeStatusControllerTests` (neu) | n8n liefert ok → API gibt Schema durch; n8n 401/Timeout → `reachable:false` mit message; Cache-Hit innerhalb 7s TTL ruft Webhook nicht erneut auf; expliziter Bypass-Test mit Cache-Eviction. |

`IHomeConnectService.GetStatusAsync()` wird im Test gemockt (Moq oder Test-Stub).

### Frontend (manuell, kein Test-Setup vorhanden)

- Smoke-Test im Browser: Power-Button-States durch Mock-Response oder direkte State-Manipulation, Modal-Flows (neu / edit / delete), Anomalie-Whitelist verifizieren.
- ESLint + `tsc --noEmit` müssen grün sein.
- Build-Container in Docker erfolgreich (`build.sh dashboard`).

---

## 8. Out of Scope

- **dashboard-s7 Anpassungen** — User integriert selbst, sobald Backend live ist (Briefing zeigt Snippet).
- **Kind-Wechsel via PUT** (Massenimport ↔ Event) — pragmatisch via Delete+Create, eigene PUT-Route nicht implementiert.
- **Background-Polling** im coffee-dashboard — bewusst on-demand, kein setInterval, keine BSH-Quota-Sorge.
- **Push/SSE für Status** — Polling reicht; HomeConnect-Events können später nachgezogen werden.
- **Editier-UI für Massenimport-Reason** — bleibt wie bisher (löschen + neu markieren).

---

## 9. Risiken & Edge Cases

- **Migration-Failure auf SQLite**: Tabellen-Rename ist in SQLite supported, sollte clean durchgehen. Auto-Baseliner kann bei Inkonsistenz aussteigen — vor Deployment lokal mit Production-DB-Kopie testen.
- **n8n-Workflow-Crash beim GET-Branch-Bau**: Power-PUT darf nicht regredieren. Workflow-Test im n8n vor Live-Schaltung.
- **BSH-OAuth-Token expired in n8n**: Workflow muss Refresh-Token-Logik haben (existiert bereits für PUT, sollte für GET wiederverwendet werden).
- **Doppel-Klick auf Power-Button**: 10s clientseitiger Cooldown + disabled state während Mutation. API-Rate-Limiting nicht im Scope.
- **Event-Annotation auf zukünftigem Datum**: nicht explizit geblockt — User soll selber wissen was er tut. Falls Edge Case auftritt, Validierung später nachziehen.
