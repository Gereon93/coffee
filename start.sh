#!/bin/bash
# Universal AI-Agent Project Bootstrap

# 1. VISION.md - DEIN Brain-Dump (Hier schreibst du alles rein!)
cat <<EOF > VISION.md
# 💡 Project Vision & Brain-Dump
## Die Idee
- [Schreib hier grob rein, was du bauen willst, z.B. Pfadfinderhütte CMS]

## Features & Wünsche
- [Feature 1, z.B. Buchungskalender]
- [Feature 2, z.B. PDF-Rechnungen]

## Tech-Präferenzen (Optional)
- [z.B. PHP/Laravel, C#, oder einfach "Schlag mir was vor"]
EOF



# 1. AGENTS.md - Definiert das Team-Verhalten (Globaler Standard)
cat <<EOF > AGENTS.md
# 👥 Agent Team Definition
- **Product Owner (PO):** Verantwortlich für Business-Logik und Akzeptanzkriterien.
- **Architect:** Plant System-Design, Datenbank-Schemata und API-Struktur.
- **Developer:** Schreibt sauberen, modularen Code (Clean Code).
- **Tester:** Erstellt Test-Suiten (TDD) und prüft Edge-Cases.
- **UX/UI:** Achtet auf User-Flows, Design-Konsistenz und Accessibility.


# 🔄 Workflow-Protokoll
1. **Kickoff:** Der PO liest VISION.md.
2. **Interview:** Der PO stellt dir 3-5 gezielte Fragen zu Lücken in deiner Vision.
3. **PO** erstellt/verfeinert die SPEC.md.
4. **UI/UX** erstellt eine User Experience DESIGN.md
5. **Architect** entwirft die ARCHITECTURE.md basierend auf der Spec.
6. **Tester** schreibt fehlschlagende Tests (Pest/PHPUnit/Jest).
7. **Developer** implementiert die Logik, bis alle Tests grün sind.
8. **Jeder Schritt** wird in der PROJECT_STATE.md dokumentiert.
9. **Jeder** stellt sicher das Dual Documentation Standard ist.


# DUAL-DOCUMENTATION STANDARD Kein Task ist abgeschlossen, ohne dass beide Seiten der Medaille dokumentiert sind:

  ##  PRODUCT.md (The "What"):

        Füge neue Features zur Liste "Available Capabilities" hinzu.

        Beschreibe kurz das "Wie prüfe ich das?" (z.B. "Öffne Swagger -> Endpoint X -> Erwartetes Ergebnis Y").
        Hier dokumentiert wir (und potenzielle Spieler/Tester), was das Ding eigentlich kann.

    Feature-Log: Was ist live? (z.B. "Battle-Simulation v1.0").

    Capabilities: Welche Aktionen kann ein Nutzer ausführen?

    Verification-Guide: "So prüfst du, ob das Feature geht" (Kurzanleitung für manuelle Tests).

 ## TECHNICAL.md (The "How"):

        Dokumentiere neue Architektur-Entscheidungen oder Domain-Änderungen.

        API-Standard: Jede neue API muss zwingend via OpenAPI (Swagger/Scalar) dokumentiert sein.
        Dies ist deine technische Bibel für die Clean Architecture.

    DDD-Struktur: Welche Aggregate und Value Objects gibt es?

    Infrastruktur: Welche APIs, Datenbanken oder Engines sind angebunden?

    ADRs (Architecture Decision Records): "Warum haben wir Engine X statt Y gewählt?"

   ## LIVING DOCS:

        Die README.md dient als zentraler Einstiegspunkt und verlinkt auf beide MD-Dateien.

EOF

# 2. CLAUDE.md - Spezifisch für Claude Code CLI
cat <<EOF > CLAUDE.md
# 🤖 Claude Code Instructions
- **Modus:** Enterprise Professional.
- **Fokus:** Erst denken/planen, dann coden. Discovery-Phase hat Priorität.
- **Regel:** Hinterfrage den User kritisch, wenn die VISION.md Lücken hat.
- **Standards:** DDD, TDD, Clean Architecture, SOLID Prinzipien.
- **Automation:** Führe Tests nach jeder Änderung automatisch aus.
- **Handover-Regel:** Aktualisiere PROJECT_STATE.md vor jedem Session-Ende oder Tool-Wechsel.
- **Stack:** [HIER STACK EINTRAGEN, z.B. Laravel, Filament, Tailwind]
EOF

# 3. DESIGN.md - Neu hinzugefügt
cat <<EOF > DESIGN.md
# 🎨 User Experience & Design
## User Flows
- [Schritt-für-Schritt Ablauf aus Nutzersicht]

## UI Komponenten
- [Welche Buttons, Formulare, Ansichten brauchen wir?]

## Design-Prinzipien
- [z.B. Mobile First, Clean, Dark Mode]
EOF

# 3. SPEC.md - Das "Was" (Template)
cat <<EOF > SPEC.md
# 📝 Project Specification: [PROJEKT NAME]
## Zielsetzung
- Kurze Beschreibung des Hauptnutzens.

## Core Features
- Feature 1...
- Feature 2...

## Business Rules (PO-Fokus)
- Regel 1...
- Regel 2...

## Out of Scope
- Was wir definitiv NICHT bauen.
EOF

# 4. ARCHITECTURE.md - Das "Wie" (Template)
cat <<EOF > ARCHITECTURE.md
# 🏗 Technical Architecture
## Tech Stack
- Frontend:
- Backend:
- Database:

## Data Model
- [Entity Name]: [Fields]

## API / Services
- Service-Layer Definitionen.
EOF

# 5. PROJECT_STATE.md - Das Gedächtnis (Überlebenswichtig!)
cat <<EOF > PROJECT_STATE.md
# 📊 Current Project State
## Aktueller Fokus
- Gerade in Arbeit: [TASK]

## Meilensteine
- [ ] Requirements (PO)
- [ ] Architecture (Architect)
- [ ] Implementation (Developer)
- [ ] Testing (Tester)

## Handover Context (Für Tool-Wechsel Claude -> Codex/Aider)
- Letzter Stand: Initiales Setup abgeschlossen.
- Nächster Schritt: PO muss Business Rules in SPEC.md definieren.
EOF

echo "🚀 Master-Template erstellt. Viel Erfolg beim Coden!"