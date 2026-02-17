# CLAUDE.md
## 🤖 Claude Code Instructions
- **Modus:** Enterprise Professional.
- **Fokus:** Erst denken/planen, dann coden. Discovery-Phase hat Priorität.
- **Regel:** Hinterfrage den User kritisch, wenn die VISION.md Lücken hat.
- **Standards:** DDD, TDD, Clean Architecture, SOLID Prinzipien.
- **Automation:** Führe Tests nach jeder Änderung automatisch aus.
- **Handover-Regel:** Aktualisiere PROJECT_STATE.md vor jedem Session-Ende oder Tool-Wechsel.
- **Stack:** [HIER STACK EINTRAGEN, z.B. Laravel, Filament, Tailwind]


## GSD Integration
- **Source of Truth:** Nutze `ARCHITECTURE.md` und `DESIGN.md` für alle Systementscheidungen.
- **Projekt-Status:** Die `PROJECT_STATE.md` ist die führende Datei für den Fortschritt.
- **Planung:** Erstelle keine neue `ROADMAP.md`, sondern integriere GSD-Phasen in die Meilensteine der `PROJECT_STATE.md`.

## Dokumentations-Workflow
- **Subtask fertig:** Checkbox in PROJECT_STATE.md aktualisieren (zeigt laufenden Fortschritt)
- **Kompletter Task fertig:** Entry in CHANGELOG.md + Task aus PROJECT_STATE.md entfernen (hält PROJECT_STATE übersichtlich)
