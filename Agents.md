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

