# EF Migrations Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Von `db.Database.EnsureCreated()` auf EF Core Migrations umstellen, ohne die Prod-DB auf der NAS zu verlieren oder Daten zu zerstören. Danach ist jedes zukünftige Schema-Change ein `dotnet ef migrations add <Name>` + Deploy.

**Architecture:** Eine initiale Migration wird generiert, die das aktuelle Schema beschreibt (entspricht dem Stand, den `EnsureCreated` heute erzeugt). Die Prod-DB bekommt manuell einen Eintrag in `__EFMigrationsHistory`, sodass EF denkt, die Initial-Migration sei bereits gelaufen — sie wird übersprungen, die Daten bleiben unberührt. Startup-Code wechselt von `EnsureCreated()` zu `Migrate()`. Tests bleiben auf InMemory mit `EnsureCreated()` (InMemory-Provider unterstützt keine Migrations).

**Tech Stack:** EF Core 9.0.0 + SQLite, `dotnet ef` CLI Tool, SQLite CLI auf der NAS.

---

## File Structure

| File | Responsibility |
|------|---------------|
| `CoffeeApi/Migrations/YYYYMMDD_Initial.cs` | Auto-generiert: CREATE TABLE für `MachineSnapshots` |
| `CoffeeApi/Migrations/AppDbContextModelSnapshot.cs` | Auto-generiert: Schema-Snapshot für Diffs |
| `CoffeeApi/Program.cs:45-51` | Switch `EnsureCreated()` → `Migrate()` |
| `CoffeeApi/CoffeeApi.csproj` | `Microsoft.EntityFrameworkCore.Design` ist schon drin (Zeile 21) — keine Änderung |
| `scripts/baseline-prod-db.sql` | Einmaliges SQL-Skript, auf NAS gegen `coffee.db` gerunnt |
| `README.md` | Neuer Abschnitt "Schema-Migrationen" |
| `PROJECT_STATE.md` | Aenderungshistorie-Eintrag |

**Tests bleiben unverändert** — `CoffeeTest/Helpers/TestDbContextFactory.cs:15` nutzt weiterhin `EnsureCreated()` auf InMemory (richtig so, InMemory-Provider kennt kein `Migrate()`).

---

## Prerequisites

- [ ] **Feature-Branch anlegen:**
  ```bash
  git checkout -b chore/ef-migrations
  ```

- [ ] **dotnet-ef Tool installieren** (falls noch nicht global vorhanden):
  ```bash
  dotnet tool install --global dotnet-ef
  # Oder falls schon da:
  dotnet tool update --global dotnet-ef
  ```
  Verify:
  ```bash
  dotnet ef --version
  ```
  Expected: 9.x.x oder neuer.

- [ ] **SSH-Zugriff zur NAS vorbereiten** — für Task 4 wird ein SQL-Command gegen `/volume2/docker_ssd/coffee-data/coffee.db` ausgeführt. Stelle sicher, dass du einloggen kannst.

---

## Task 1: Lokale Dev-DB vor dem Umbau dokumentieren

Bevor wir irgendwas ändern, snapshoten wir das aktuelle Schema, damit wir die generierte Migration dagegen vergleichen können.

**Files:**
- Create: `scripts/current-schema.sql` (temporär, wird nach Task 3 gelöscht)

- [ ] **Step 1: Lokale API mit leerer DB einmal starten**

```bash
cd CoffeeApi
rm -f coffee.db  # Falls vorhanden
dotnet run &
SERVER_PID=$!
sleep 5
curl -s http://localhost:5000/api/health | head -c 200
kill $SERVER_PID
cd ..
```

Expected: `{"status":"healthy",...}` — DB wurde erstellt.

- [ ] **Step 2: Schema exportieren**

```bash
mkdir -p scripts
sqlite3 CoffeeApi/coffee.db .schema > scripts/current-schema.sql
cat scripts/current-schema.sql
```

Expected: CREATE TABLE `MachineSnapshots` mit allen Spalten aus `Domain/MachineSnapshot.cs`, plus 3 Indexe (`IX_MachineSnapshots_Timestamp`, `IX_MachineSnapshots_MachineId`, `IX_MachineSnapshots_Idempotency`).

**Diese Datei nicht committen** — sie ist nur Arbeitsmaterial für Task 3.

---

## Task 2: Initiale Migration generieren

**Files:**
- Create: `CoffeeApi/Migrations/<timestamp>_Initial.cs`
- Create: `CoffeeApi/Migrations/<timestamp>_Initial.Designer.cs`
- Create: `CoffeeApi/Migrations/AppDbContextModelSnapshot.cs`

- [ ] **Step 1: Migration generieren**

```bash
cd CoffeeApi
dotnet ef migrations add Initial
cd ..
```

Expected: drei neue Dateien im Ordner `CoffeeApi/Migrations/` erscheinen. Der Zeitstempel hängt vom Ausführungszeitpunkt ab — im Folgenden **merke dir den Migration-Namen** (z.B. `20260418120000_Initial`).

- [ ] **Step 2: Migration-ID in ENV-Var speichern (für spätere Tasks)**

```bash
MIGRATION_ID=$(ls CoffeeApi/Migrations | grep '_Initial.cs$' | head -1 | sed 's/.cs$//')
echo "$MIGRATION_ID"
echo "export MIGRATION_ID=$MIGRATION_ID" > .migration-id.env
cat .migration-id.env
```

Expected: `export MIGRATION_ID=20260418xxxxxx_Initial` — diese Datei ist temporär für den lokalen Workflow, wird nicht committet.

- [ ] **Step 3: .gitignore um `.migration-id.env` erweitern**

Füge in `.gitignore` hinzu (falls nicht schon drin):

```
.migration-id.env
scripts/current-schema.sql
```

---

## Task 3: Generierte Migration gegen bestehendes Schema verifizieren

Hier wird sichergestellt, dass die generierte Migration **dasselbe** Schema produziert wie bisher `EnsureCreated()`. Ein Unterschied wäre gefährlich — dann würde eine frische DB anders aussehen als Prod.

**Files:**
- Verify only: `CoffeeApi/Migrations/<timestamp>_Initial.cs`

- [ ] **Step 1: Migration-SQL generieren**

```bash
cd CoffeeApi
dotnet ef migrations script --output ../scripts/migration-initial.sql
cd ..
cat scripts/migration-initial.sql
```

Expected: SQL-Statements, die `__EFMigrationsHistory` anlegen, dann `CREATE TABLE "MachineSnapshots"`, dann Indexe, dann den History-INSERT.

- [ ] **Step 2: Vergleichen**

Öffne beide Dateien nebeneinander:
- `scripts/current-schema.sql` (von Task 1, Stand „echt in DB")
- `scripts/migration-initial.sql` (was die Migration generieren würde)

**Was muss identisch sein:**
- Spaltendefinitionen (Name, Typ, Nullability, Default)
- Primary Key
- Alle drei Indexe (`IX_MachineSnapshots_Timestamp`, `IX_MachineSnapshots_MachineId`, `IX_MachineSnapshots_Idempotency`)

**Was unterscheidet sich erwartbar:**
- `migration-initial.sql` hat zusätzlich `__EFMigrationsHistory`-Tabelle und INSERT — das ist gewollt, damit die Migration sich selbst als "gelaufen" markiert.
- Reihenfolge der Spalten kann variieren — das ist OK, solange die Spalten gleich sind.

- [ ] **Step 3: Bei Abweichung — Entity-Config fixen**

Falls z.B. `HasDefaultValue("EQ900-DEFAULT")` für `MachineId` in der Migration fehlt, liegt's an `OnModelCreating` in `AppDbContext.cs`. Korrigieren, Migration neu generieren:

```bash
cd CoffeeApi
dotnet ef migrations remove
# OnModelCreating anpassen
dotnet ef migrations add Initial
cd ..
# Neu vergleichen (Schritte 1-2 wiederholen)
```

- [ ] **Step 4: Aufräum-Dateien löschen**

```bash
rm scripts/current-schema.sql scripts/migration-initial.sql
```

- [ ] **Step 5: Commit**

```bash
git add CoffeeApi/Migrations .gitignore
git commit -m "chore(db): add EF initial migration matching current EnsureCreated schema"
```

---

## Task 4: Baseline-Skript für Prod-DB schreiben

**Files:**
- Create: `scripts/baseline-prod-db.sql`

- [ ] **Step 1: Skript anlegen**

```bash
source .migration-id.env
mkdir -p scripts
cat > scripts/baseline-prod-db.sql <<EOF
-- Baseline-Script: markiert die bestehende Prod-DB als bereits migriert.
-- Ausführen nur EINMAL auf der Prod-DB unter /volume2/docker_ssd/coffee-data/coffee.db
-- Vorher: coffee.db sichern!

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('${MIGRATION_ID}', '9.0.0');

SELECT * FROM "__EFMigrationsHistory";
EOF

cat scripts/baseline-prod-db.sql
```

Expected: SQL-Datei, die als `MigrationId` den Wert aus Task 2 enthält (z.B. `20260418xxxxxx_Initial`).

- [ ] **Step 2: Lokal gegen Kopie der Dev-DB testen**

```bash
cp CoffeeApi/coffee.db /tmp/coffee-test.db
sqlite3 /tmp/coffee-test.db < scripts/baseline-prod-db.sql
sqlite3 /tmp/coffee-test.db "SELECT * FROM __EFMigrationsHistory;"
```

Expected: eine Zeile mit der Migration-ID und ProductVersion 9.0.0.

```bash
rm /tmp/coffee-test.db
```

- [ ] **Step 3: Commit**

```bash
git add scripts/baseline-prod-db.sql
git commit -m "chore(db): add prod-db baseline SQL for EF migration rollout"
```

---

## Task 5: Program.cs von EnsureCreated auf Migrate umstellen

**Files:**
- Modify: `CoffeeApi/Program.cs:46-51`

- [ ] **Step 1: Test — Startup darf nicht crashen mit migrierter DB**

Da wir keinen Integration-Test für Startup haben, schreiben wir einen minimalen. (Der bestehende `CoffeeTest` arbeitet mit InMemory und würde `Migrate()` nicht mal sehen, daher ist das folgende ein Sanity-Check per Skript, kein xUnit-Test.)

Erstelle temporäres Skript:

```bash
cat > /tmp/test-migrate.sh <<'EOF'
#!/bin/bash
set -e
cd CoffeeApi

# 1. Szenario A: leere DB → Migrate() muss Schema anlegen
rm -f /tmp/coffee-migrate-test.db
export ConnectionStrings__Default="Data Source=/tmp/coffee-migrate-test.db"
dotnet run &
PID=$!
sleep 5
curl -sf http://localhost:5000/api/health > /dev/null || (kill $PID; exit 1)
kill $PID
sleep 1

sqlite3 /tmp/coffee-migrate-test.db "SELECT COUNT(*) FROM __EFMigrationsHistory;" | grep -q '^1$' || { echo "History entry missing"; exit 1; }
sqlite3 /tmp/coffee-migrate-test.db "SELECT name FROM sqlite_master WHERE type='table' AND name='MachineSnapshots';" | grep -q MachineSnapshots || { echo "MachineSnapshots missing"; exit 1; }

# 2. Szenario B: DB mit Daten + History-Row → Migrate() darf Daten nicht anfassen
rm -f /tmp/coffee-migrate-test.db
# Simuliere Baseline-Szenario: manuell schema + 1 row + history
dotnet run &
PID=$!
sleep 5
kill $PID
sleep 1
# Daten einfügen
sqlite3 /tmp/coffee-migrate-test.db "INSERT INTO MachineSnapshots (Timestamp, MachineId, BeverageCounterCoffee, BeverageCounterCoffeeAndMilk, BeverageCounterMilk, BeverageCounterHotWaterCups, BeverageCounterHotWater, OperationState, RemoteControlAllowed, LocalControlActive, InteriorIlluminationActive, CreatedAt) VALUES (datetime('now'), 'TEST', 42, 0, 0, 0, 0, 'Ready', 0, 0, 0, datetime('now'));"
BEFORE=$(sqlite3 /tmp/coffee-migrate-test.db "SELECT COUNT(*) FROM MachineSnapshots;")
# Restart
dotnet run &
PID=$!
sleep 5
curl -sf http://localhost:5000/api/health > /dev/null || (kill $PID; exit 1)
kill $PID
AFTER=$(sqlite3 /tmp/coffee-migrate-test.db "SELECT COUNT(*) FROM MachineSnapshots;")

if [ "$BEFORE" != "$AFTER" ]; then echo "Data count changed: $BEFORE -> $AFTER"; exit 1; fi
echo "OK: schema created, data preserved"
EOF
chmod +x /tmp/test-migrate.sh
```

(Nicht ausführen. Erst nach Step 2 Ergebnis gegenchecken.)

- [ ] **Step 2: Program.cs ändern**

Finde in `CoffeeApi/Program.cs:46-51`:

```csharp
// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
```

Ersetze durch:

```csharp
// Apply pending EF migrations (no-op if DB is up to date)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
```

- [ ] **Step 3: Skript aus Step 1 ausführen**

```bash
/tmp/test-migrate.sh
```

Expected: `OK: schema created, data preserved`. Wenn rot: Log lesen, meistens entweder Migrate-Crash (Connection-String falsch) oder DB-Lock. `/tmp/coffee-migrate-test.db` löschen und nochmal.

- [ ] **Step 4: Unit-Tests laufen lassen**

```bash
dotnet test CoffeeTest/CoffeeTest.csproj
```

Expected: 33 Tests grün. Wenn rot — `TestDbContextFactory` nutzt `EnsureCreated()` auf InMemory, das bleibt unverändert und sollte weiter funktionieren.

- [ ] **Step 5: Cleanup + Commit**

```bash
rm /tmp/test-migrate.sh /tmp/coffee-migrate-test.db
git add CoffeeApi/Program.cs
git commit -m "feat(db): use Migrate() instead of EnsureCreated() for schema rollout"
```

---

## Task 6: Dokumentation

**Files:**
- Modify: `README.md` (neuer Abschnitt)
- Modify: `PROJECT_STATE.md`

- [ ] **Step 1: README-Abschnitt "Schema-Migrationen"**

Nach dem Abschnitt "Datensicherung" (`README.md:203-211`) anfügen:

```markdown
## Schema-Migrationen

Das Backend nutzt **EF Core Migrations**. Beim Container-Start wird `Database.Migrate()` ausgeführt — pending Migrations werden angewendet, bestehende Daten bleiben unberührt.

### Neue Migration anlegen

```bash
cd CoffeeApi
dotnet ef migrations add <NameDerAenderung>
# Migration-Dateien committen → beim nächsten Deploy läuft sie automatisch
```

### Prod-DB erst-baseline (einmalig, erledigt 2026-04-18)

Die bestehende Prod-DB wurde mit `scripts/baseline-prod-db.sql` als "Initial bereits migriert" markiert, ohne Datenverlust. Das Skript bleibt im Repo als Referenz.

### Tests und Migrations

Tests in `CoffeeTest/` nutzen `InMemoryDatabase` — dieses Provider-Backend unterstützt keine Migrations. `TestDbContextFactory.Create()` nutzt daher bewusst weiterhin `EnsureCreated()`. Das ist korrekt und muss so bleiben.
```

- [ ] **Step 2: PROJECT_STATE.md ergänzen**

In der Aenderungshistorie-Tabelle (ab Zeile 170):

```markdown
| 2026-04-18 | EF Core Migrations aktiviert: Initial-Migration + Prod-DB baselined |
```

- [ ] **Step 3: Commit**

```bash
git add README.md PROJECT_STATE.md
git commit -m "docs: add schema-migration workflow section"
```

---

## Task 7: Deployment auf Prod-NAS (nicht automatisierbar)

Dieser Schritt wird **per Hand auf der NAS** ausgeführt. Er ist sicherheitskritisch — ein Fehler bedeutet Datenverlust oder blockierten App-Start. Alle Schritte einzeln abhaken.

- [ ] **Step 1: Feature-Branch mergen**

Per Merge-Request `chore/ef-migrations` → `main`. Pipeline aus Plan 1 baut neue Images.

- [ ] **Step 2: Prod-DB sichern** (SSH zur NAS)

```bash
ssh <nas>
sudo cp /volume2/docker_ssd/coffee-data/coffee.db /volume2/docker_ssd/coffee-data/coffee.db.pre-migration-2026-04-18
ls -lh /volume2/docker_ssd/coffee-data/coffee.db*
```

Expected: Backup-File existiert, Größe > 0.

- [ ] **Step 3: API-Container stoppen**

Im Portainer UI oder via CLI:

```bash
docker stop coffee-api
```

- [ ] **Step 4: Baseline-SQL laufen lassen**

```bash
cd /tmp
curl -O <gitlab-raw-url>/scripts/baseline-prod-db.sql
# Oder SCP von Entwicklungsrechner:
# scp scripts/baseline-prod-db.sql <nas>:/tmp/

sudo sqlite3 /volume2/docker_ssd/coffee-data/coffee.db < /tmp/baseline-prod-db.sql
sudo sqlite3 /volume2/docker_ssd/coffee-data/coffee.db "SELECT * FROM __EFMigrationsHistory;"
```

Expected: eine Zeile mit `<timestamp>_Initial` und Produktversion.

- [ ] **Step 5: API-Container starten (neues Image mit Migrate()) **

In Portainer den Stack neu deployen oder:

```bash
docker pull 192.168.2.143:5050/gereon/coffee/coffee-api:latest
docker start coffee-api
docker logs -f coffee-api --tail 50
```

Expected Log-Output: Keine Fehler, kein "table MachineSnapshots already exists", kein "database is locked".

- [ ] **Step 6: Health-Check**

```bash
curl -s http://192.168.2.143:8089/api/health
```

Expected: `{"status":"healthy","database":"connected","lastSnapshot":"..."}` mit einem lastSnapshot-Timestamp, der NICHT null ist (= Daten noch da).

- [ ] **Step 7: Snapshot-Count prüfen**

```bash
sudo sqlite3 /volume2/docker_ssd/coffee-data/coffee.db "SELECT COUNT(*) FROM MachineSnapshots;"
```

Vergleichen mit Count aus Step 2 (Backup-DB):

```bash
sudo sqlite3 /volume2/docker_ssd/coffee-data/coffee.db.pre-migration-2026-04-18 "SELECT COUNT(*) FROM MachineSnapshots;"
```

Expected: beide identisch.

- [ ] **Step 8: Dashboard laden + smoke test**

Öffne `http://192.168.2.143:8090` im Browser. Das Dashboard sollte lazy seine Daten laden wie vorher. Der Log-Tab sollte dieselbe Zeilenzahl wie vor dem Deployment zeigen.

- [ ] **Step 9: Rollback-Plan bereit halten**

Falls irgendwas schief geht in Steps 5-8:

```bash
docker stop coffee-api
sudo cp /volume2/docker_ssd/coffee-data/coffee.db.pre-migration-2026-04-18 /volume2/docker_ssd/coffee-data/coffee.db
docker pull 192.168.2.143:5050/gereon/coffee/coffee-api:<previous-sha>  # Sha vom Vorgänger-Deploy
docker start coffee-api
```

---

## Self-Review

- ✅ **Spec coverage:** EnsureCreated → Migrate (Task 5), Baseline-SQL (Task 4), Prod-Deploy mit Backup+Rollback (Task 7), Tests bleiben grün (Task 5 Step 4), Doku (Task 6).
- ✅ **Placeholder scan:** keine TBD, alle Commands vollständig. MIGRATION_ID ist placeholder-haft, aber der Wert wird in Task 2 erzeugt und in Task 4 eingesetzt — das ist ein legitimer Wert, nicht "TODO".
- ✅ **Type consistency:** `db.Database.Migrate()` in Program.cs, `EnsureCreated()` im Test-Helper — das ist bewusst unterschiedlich und wird in README Task 6 Step 1 dokumentiert.
