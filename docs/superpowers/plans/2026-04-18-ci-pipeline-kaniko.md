# CI Pipeline Kaniko Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GitLab CI läuft wieder und baut+pusht `coffee-api` + `coffee-dashboard` Container-Images zur `:5050` Registry bei jedem Push auf `main`.

**Architecture:** `.gitlab-ci.yml` wird komplett umgeschrieben. Base-Image wechselt vom toten `192.168.2.143:3033/dotnet-9-special` zu `mcr.microsoft.com/dotnet/sdk:10.0-preview`. Zwei neue Build-Jobs nutzen Kaniko (daemonless, kein DinD nötig), die das Muster aus `reddit-finance` spiegeln. `build.sh` bleibt als lokaler Fallback bestehen.

**Tech Stack:** GitLab CI, Kaniko (`gcr.io/kaniko-project/executor:debug`), Microsoft .NET 10 SDK Image, GitLab Container Registry (`$CI_REGISTRY_IMAGE` → `192.168.2.143:5050/gereon/coffee`).

---

## File Structure

| File | Responsibility |
|------|---------------|
| `.gitlab-ci.yml` | Pipeline-Definition: test + sonarqube + zwei kaniko build-jobs |
| `README.md` | Deployment-Section: CI ist primärer Weg, `build.sh` Fallback |
| `PROJECT_STATE.md` | Backlog-Item "GitLab Runner mit .NET 10 SDK" als erledigt markieren |

---

## Prerequisites (einmal klären, bevor Task 1 startet)

- [ ] **Bestätige:** GitLab Registry auf `192.168.2.143:5050` ist die projekt-eigene Registry → `$CI_REGISTRY_USER`/`$CI_REGISTRY_PASSWORD` funktionieren ohne extra Secret.
- [ ] **Bestätige:** Runner hat Internet-Zugriff für `gcr.io/kaniko-project/executor:debug` und `mcr.microsoft.com/dotnet/sdk:10.0-preview`.
- [ ] **Feature-Branch anlegen** — alle Tasks entstehen auf `ci/kaniko-build`, nicht auf `main`:
  ```bash
  git checkout -b ci/kaniko-build
  ```

---

## Task 1: Base-Image auf .NET 10 + Struktur aufräumen

**Files:**
- Modify: `.gitlab-ci.yml` (komplette Neufassung)

- [ ] **Step 1: Backup der alten Datei (nur zur Sicherheit)**

```bash
cp .gitlab-ci.yml .gitlab-ci.yml.bak
```

- [ ] **Step 2: Datei komplett ersetzen**

Überschreibe `.gitlab-ci.yml` mit folgendem Inhalt. Die vier Stages bleiben konzeptionell (test → sonarqube → build), aber Base-Image und Struktur werden auf den Kaniko-Stil aus `reddit-finance` angeglichen.

```yaml
stages:
  - test
  - sonarqube-check
  - sonarqube-vulnerability-report
  - build

variables:
  coffee_solution: "Coffee.sln"
  test_project: "CoffeeTest/CoffeeTest.csproj"
  IMAGE_API: $CI_REGISTRY_IMAGE/coffee-api
  IMAGE_DASHBOARD: $CI_REGISTRY_IMAGE/coffee-dashboard

# ─── TEST STAGE ──────────────────────────────────────
test:
  stage: test
  image: mcr.microsoft.com/dotnet/sdk:10.0-preview
  cache:
    key: nuget
    paths:
      - ~/.nuget/packages/
    policy: pull-push
  before_script:
    - dotnet restore $coffee_solution
  script:
    - dotnet build $coffee_solution --no-restore
    - dotnet test $test_project --no-restore
  rules:
    - if: $CI_MERGE_REQUEST_ID
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
    - if: $CI_COMMIT_BRANCH == "dev"

# ─── SONARQUBE ───────────────────────────────────────
sonarqube-check:
  stage: sonarqube-check
  image: mcr.microsoft.com/dotnet/sdk:10.0-preview
  variables:
    SONAR_USER_HOME: "${CI_PROJECT_DIR}/.sonar"
    GIT_DEPTH: "0"
  cache:
    key: "${CI_JOB_NAME}"
    paths:
      - .sonar/cache
  script:
    - dotnet tool install --global dotnet-sonarscanner
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - dotnet sonarscanner begin /k:"gereon_coffee_ba5c47bb-a0a1-4e5a-b9d6-2187da863b11" /d:sonar.token="$SONAR_TOKEN" /d:"sonar.host.url=$SONAR_HOST_URL"
    - dotnet build
    - dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
  allow_failure: true
  rules:
    - if: $CI_MERGE_REQUEST_ID
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
    - if: $CI_COMMIT_BRANCH == "dev"

sonarqube-vulnerability-report:
  stage: sonarqube-vulnerability-report
  image: mcr.microsoft.com/dotnet/sdk:10.0-preview
  script:
    - curl -u "${SONAR_TOKEN}:" "${SONAR_HOST_URL}/api/issues/gitlab_sast_export?projectKey=gereon_coffee_ba5c47bb-a0a1-4e5a-b9d6-2187da863b11&branch=${CI_COMMIT_BRANCH}&pullRequest=${CI_MERGE_REQUEST_IID}" -o gl-sast-sonar-report.json
  allow_failure: true
  rules:
    - if: $CI_MERGE_REQUEST_ID
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
    - if: $CI_COMMIT_BRANCH == "dev"
  artifacts:
    expire_in: 1 day
    reports:
      sast: gl-sast-sonar-report.json
  dependencies:
    - sonarqube-check
```

**Hinweise zur Änderung:**
- `image:` auf Top-Level entfernt — jeder Job setzt sein eigenes Image. Vermeidet, dass Kaniko-Jobs das .NET SDK Image erben.
- `before_script` entfernt — das triggerte `dotnet restore` auch in sonarqube-Jobs.
- `rules:` statt `only:` (modernerer GitLab-Syntax, matcht `reddit-finance`).
- `--no-restore` im test-Job: spart 30 Sekunden, da `before_script` bereits restored hat.
- `.NET 10 preview` gewählt weil `CoffeeApi/Dockerfile:2` dieselbe Tag-Variante nutzt → keine SDK-Drift.

- [ ] **Step 3: YAML-Syntax validieren**

```bash
python3 -c "import yaml; yaml.safe_load(open('.gitlab-ci.yml'))" && echo OK
```

Expected: `OK`.

- [ ] **Step 4: Commit**

```bash
git add .gitlab-ci.yml
rm .gitlab-ci.yml.bak
git commit -m "ci: replace dead :3033 base image with .NET 10 SDK, rules-based triggers"
```

---

## Task 2: Kaniko Build-Job für Coffee API

**Files:**
- Modify: `.gitlab-ci.yml` (anhängen neuer Job `build-api`)

- [ ] **Step 1: Job anhängen**

Am Ende der `.gitlab-ci.yml` einfügen:

```yaml
# ─── BUILD + PUSH: API ───────────────────────────────
build-api:
  stage: build
  image:
    name: gcr.io/kaniko-project/executor:debug
    entrypoint: [""]
  script:
    - HASH=${CI_COMMIT_SHORT_SHA}
    - mkdir -p /kaniko/.docker
    - echo "{\"auths\":{\"192.168.2.143:5050\":{\"auth\":\"$(echo -n ${CI_REGISTRY_USER}:${CI_REGISTRY_PASSWORD} | base64)\"}}}" > /kaniko/.docker/config.json
    - /kaniko/executor
        --context "$CI_PROJECT_DIR/CoffeeApi"
        --dockerfile "$CI_PROJECT_DIR/CoffeeApi/Dockerfile"
        --destination "$IMAGE_API:$HASH"
        --destination "$IMAGE_API:latest"
        --insecure-registry=192.168.2.143:5050
        --build-arg "BUILD_COMMIT=$HASH"
    - echo "Pushed $IMAGE_API:$HASH + $IMAGE_API:latest"
  rules:
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
```

**Hinweis:** `--context` ist `CoffeeApi/`, nicht Projekt-Root — das Dockerfile referenziert relative Pfade zu seinen `.csproj`-Nachbarn in dieser Dir.

- [ ] **Step 2: YAML validieren**

```bash
python3 -c "import yaml; yaml.safe_load(open('.gitlab-ci.yml'))" && echo OK
```

- [ ] **Step 3: Commit**

```bash
git add .gitlab-ci.yml
git commit -m "ci: add kaniko build-api job (main only, pushes :hash + :latest)"
```

---

## Task 3: Kaniko Build-Job für Coffee Dashboard

**Files:**
- Modify: `.gitlab-ci.yml` (anhängen `build-dashboard`)

- [ ] **Step 1: Job anhängen**

Am Ende der `.gitlab-ci.yml`:

```yaml
# ─── BUILD + PUSH: Dashboard ─────────────────────────
build-dashboard:
  stage: build
  image:
    name: gcr.io/kaniko-project/executor:debug
    entrypoint: [""]
  script:
    - HASH=${CI_COMMIT_SHORT_SHA}
    - mkdir -p /kaniko/.docker
    - echo "{\"auths\":{\"192.168.2.143:5050\":{\"auth\":\"$(echo -n ${CI_REGISTRY_USER}:${CI_REGISTRY_PASSWORD} | base64)\"}}}" > /kaniko/.docker/config.json
    - /kaniko/executor
        --context "$CI_PROJECT_DIR/coffee-dashboard"
        --dockerfile "$CI_PROJECT_DIR/coffee-dashboard/Dockerfile"
        --destination "$IMAGE_DASHBOARD:$HASH"
        --destination "$IMAGE_DASHBOARD:latest"
        --insecure-registry=192.168.2.143:5050
        --build-arg "BUILD_COMMIT=$HASH"
    - echo "Pushed $IMAGE_DASHBOARD:$HASH + $IMAGE_DASHBOARD:latest"
  rules:
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
```

- [ ] **Step 2: YAML validieren**

```bash
python3 -c "import yaml; yaml.safe_load(open('.gitlab-ci.yml'))" && echo OK
```

- [ ] **Step 3: Commit**

```bash
git add .gitlab-ci.yml
git commit -m "ci: add kaniko build-dashboard job (main only)"
```

---

## Task 4: Feature-Branch pushen + Pipeline auf main-Branch simulieren

Da Test-Jobs bei Merge-Requests und Sonarqube auf allen Branches laufen, aber Build-Jobs nur auf `main`, müssen wir beides verifizieren.

- [ ] **Step 1: Feature-Branch pushen**

```bash
git push -u origin ci/kaniko-build
```

- [ ] **Step 2: Pipeline beobachten**

- Öffne GitLab UI → Pipelines. Die `test`-, `sonarqube-check`- und `sonarqube-vulnerability-report`-Jobs sollten laufen.
- Erwartet: `test` grün, `sonarqube-*` entweder grün oder allow-failure-gelb.
- Erwartet: **keine** build-Jobs (weil noch nicht auf main).

- [ ] **Step 3: Bei Fehler — diagnostizieren**

Wenn `test` rot wird: Job-Log öffnen und checken. Wahrscheinliche Ursachen:
- `.NET 10 preview` Tag existiert nicht mehr → auf `mcr.microsoft.com/dotnet/sdk:10.0` wechseln
- NuGet-Restore scheitert → Cache ggf. leeren in GitLab

Fix im selben Branch committen und pushen, bis grün.

- [ ] **Step 4: Merge Request öffnen** (dadurch laufen nochmal alle MR-Trigger)

```bash
# Im GitLab UI: "Create merge request" von ci/kaniko-build → main
# Oder via glab CLI, falls installiert
```

Erwartet: MR-Pipeline grün (oder Sonar allow-failure gelb).

---

## Task 5: Auf main mergen + Build-Jobs verifizieren

- [ ] **Step 1: MR mergen**

Im GitLab UI auf "Merge" klicken. Der Merge-Commit triggert eine neue Pipeline auf `main`.

- [ ] **Step 2: Build-Jobs beobachten**

Auf `main` müssen jetzt laufen:
- `test` (grün)
- `sonarqube-check` / `sonarqube-vulnerability-report` (grün oder allow-failure gelb)
- `build-api` (grün) ← neu
- `build-dashboard` (grün) ← neu

Die Build-Jobs loggen am Ende `Pushed ...:$HASH + ...:latest`.

- [ ] **Step 3: Registry checken**

```bash
# Via GitLab UI: Packages & Registries → Container Registry
# Beide Repositories sollten neue Tags haben:
#   coffee-api:<short-sha>, coffee-api:latest
#   coffee-dashboard:<short-sha>, coffee-dashboard:latest
```

- [ ] **Step 4: Bei Fehler in build-Job**

Häufigste Ursachen:
- **Auth failed:** `$CI_REGISTRY_USER`/`$CI_REGISTRY_PASSWORD` nicht verfügbar → in GitLab prüfen: Settings → CI/CD → Variables. Für die GitLab-eigene Registry werden sie automatisch gesetzt, außer `Deploy Tokens` wurden separat konfiguriert.
- **Insecure Registry abgelehnt:** `--insecure-registry=192.168.2.143:5050` muss korrekt gesetzt sein (ist er). Falls HTTPS erzwungen wird, mit Registry-Admin klären.
- **Dockerfile-Context:** Wenn `--context ./CoffeeApi` fehlt und Context = Repo-Root ist, kopiert Kaniko zuviel. Auf `$CI_PROJECT_DIR/CoffeeApi` bestehen.

---

## Task 6: README + PROJECT_STATE aktualisieren

**Files:**
- Modify: `README.md` (Deployment-Section)
- Modify: `PROJECT_STATE.md` (Backlog-Item entfernen, Aenderungshistorie ergänzen)

- [ ] **Step 1: README.md — "Docker Deployment (Produktion)" Section umbauen**

Lies `README.md:32-72`. Ersetze den Abschnitt (inkl. `./build.sh`-Block) durch:

```markdown
### Docker Deployment (Produktion)

**Container-Images** werden automatisch von der GitLab-Pipeline gebaut und gepusht, sobald auf `main` gemerget wird:

- `192.168.2.143:5050/gereon/coffee/coffee-api:latest`
- `192.168.2.143:5050/gereon/coffee/coffee-dashboard:latest`

Zusätzlich wird jeder Build mit `:${CI_COMMIT_SHORT_SHA}` getaggt für Rollbacks.

**Fallback für lokale Builds** (wenn die CI nicht verfügbar ist):

```bash
./build.sh all           # Baut API + Dashboard lokal, pusht zur Registry
./build.sh api           # Nur API
./build.sh dashboard     # Nur Dashboard
./build.sh api --no-push # Nur bauen, nicht pushen
```

**Docker Compose in Portainer deployen:**

<!-- rest of compose YAML unchanged -->
```

Behalte den bestehenden `docker-compose.yml`-Block (ab `services:`) unverändert.

- [ ] **Step 2: PROJECT_STATE.md aktualisieren**

Entferne das Backlog-Item "GitLab Runner mit .NET 10 SDK Image fuer vollautomatische CI/CD" aus der Liste ab Zeile 64.

Ergänze in der Aenderungshistorie:

```markdown
| 2026-04-18 | CI-Pipeline reaktiviert: Kaniko baut API+Dashboard auf main |
```

- [ ] **Step 3: Commit + push auf main**

```bash
git add README.md PROJECT_STATE.md
git commit -m "docs: update deployment section — CI builds images, build.sh as fallback"
git push origin main
```

Erwartet: Die Pipeline läuft erneut auf main, Build-Jobs produzieren neue Tags (die docs-Commit enthält aber keine Dockerfile-Änderung → Images sind bis auf Tag identisch, das ist OK).

---

## Self-Review (erledigt beim Schreiben des Plans)

- ✅ **Spec coverage:** Base-Image getauscht (Task 1), Build-Jobs hinzugefügt (Tasks 2-3), Main-only-Trigger (Tasks 2-3 rules), Deployment verifiziert (Task 5), Doku aktualisiert (Task 6).
- ✅ **Placeholder scan:** keine TBD/implement-later.
- ✅ **Type consistency:** Variable-Namen `$IMAGE_API` / `$IMAGE_DASHBOARD` in Tasks 1-3 konsistent. `$CI_COMMIT_SHORT_SHA` überall gleich geschrieben.
