# Hermes Gatekeeper — Phase 12y Backend Architecture Remediation

## Context

Diese Phase ist eine bewusst vorgezogene Sanierungsphase vor weiterer Produktarbeit.

Auslöser:

- kritisches Architecture Review des Gatekeeper-Backends
- offener, unsauberer Working Tree im Catalog/DI-Bereich
- zentrale Architekturprobleme auf Sicherheits-, Execution-, Grant- und Audit-Grenzen

Diese Phase ist backend-only. Sie ist keine neue Produktfunktion, sondern die technische Stabilisierung des bestehenden Kerns, bevor weitere Produktarbeit darauf gestapelt wird.

## Warum diese Phase jetzt kommt

Die wichtigsten Review-Befunde sitzen in zentralen Durchsetzungspunkten:

- Authentifizierung/Autorisierung ist framework-seitig registriert, aber faktisch über `AllowAnonymous()` + manuelle Guards pro Endpoint gebaut.
- `SessionActionService` ist ein God Service, der SSH-Pfad, Legacy-/Dummy-Pfad, Audit, Budget-Reservierung und Fehlerabbildung zugleich trägt.
- Der DB-first-SSH-Katalog ist im Code noch kein sauberer Single Source of Truth.
- SSH-Grants/Profile werden implizit aus Capability-Strings und Single-Target-Sonderfällen abgeleitet.
- Der Audit-Fluss serialisiert in Application JSON und parst in Infrastructure wieder zurück.
- Es gibt mehr Schicht-/Repository-/DTO-Ceremony als für den tatsächlichen Monolithen sinnvoll ist.

Wenn weitere Produktfeatures darauf aufgebaut werden, verbreitern wir diese Probleme statt sie zu beseitigen.

## Binding Decisions

### Entscheidung 1 — Eigene Sanierungsphase vor weiterer Produktarbeit

Gewählt: Ja.

Konsequenz:

- Weitere Produktarbeit wird nicht auf den aktuellen Architekturproblemen aufgebaut.
- Die Sanierung bekommt eigene Commit- und Review-Grenzen.

### Entscheidung 2 — Working Tree erst sichern, dann sanieren

Gewählt: Ja.

Konsequenz:

- Die offenen Phase-12.x-/Catalog-Änderungen werden zuerst organisatorisch isoliert.
- Keine Sanierungsimplementierung auf einem unsauberen Working Tree.

### Entscheidung 3 — Auth Boundary zuerst

Gewählt: Ja.

Konsequenz:

- Vor allen internen Refactors wird die Auth-/Authorization-Grenze framework-native repariert.
- `AllowAnonymous()` + manuelle Primär-Auth-Guards sollen aus den geschützten Endpoints verschwinden.

### Entscheidung 4 — Service-Split vor Catalog-Finalisierung

Gewählt: Ja.

Konsequenz:

- Zuerst wird der Ausführungskern entkoppelt.
- Danach werden Grants und DB-first-Truthfulness bereinigt.

### Entscheidung 5 — Explizites Grant-Modell statt Capability-Heuristik

Gewählt: Ja.

Konsequenz:

- SSH-Profile/Grants werden als echte Fachlichkeit modelliert.
- Keine Ableitung mehr über `ssh.`-/`remote.`-Prefix und keine `Targets.Count == 1`-Sonderregel als Modellanker.

### Entscheidung 6 — Ceremony-Abbau zuletzt und gezielt

Gewählt: Ja.

Konsequenz:

- Kein Big-Bang-Rewrite der gesamten Architektur.
- Erst Verhalten, Grenzen und Wahrheitspfade stabilisieren.
- Danach gezielt Abstraktionen abbauen, die keinen echten Nutzen liefern.

## Scope

### In Scope

- Auth Boundary Repair
- Entflechtung des Session-Action-Ausführungskerns
- Typisierung/Begradigung des Audit-Flusses
- explizites SSH-Grant-/Profilmodell
- echte DB-first-Truthfulness für Approval-Validation und Runtime-Resolve
- gezielter Abbau überflüssiger Ceremony nach Stabilisierung

### Out of Scope

- Frontend-Refactor
- neue Produktfeatures
- neue Connectoren/Adapter
- OIDC, TOTP, Passkeys/WebAuthn, mTLS, Multi-Admin Approval
- Policy-Editing-UI
- freie Shell / Break-glass-Ausbau
- Big-Bang-Abschaffung aller bestehenden Schichten

## Phasen

## Phase S0 — Arbeitsbasis sichern

### Ziel

Den offenen Working Tree sauber isolieren, bevor Architektur-Sanierung umgesetzt wird.

### Änderungen

- aktuelle offene Catalog-/DI-/Bootstrap-Änderungen in Branch/Snapshot sichern
- Sanierungsarbeit auf sauberem Branch beginnen
- dokumentieren, welcher Stand validierter Working-Tree-Kontext ist und was davon noch nicht committed ist

### Ergebnis

- klare Commit-Grenze
- saubere Review-Basis
- keine Vermischung von Product-Work und Sanierung

### Validierung

- Snapshot/Branch der offenen Änderungen vorhanden
- sauberer Ausgangs-Branch für Sanierung vorhanden
- Doku aktualisiert, welche offenen Änderungen später reintegriert werden

## Phase S1 — Auth Boundary Repair

### Ziel

Auth/Authorization soll an der Framework-Grenze sichtbar und erzwungen werden, nicht primär im Handler-Code.

### Änderungen

- geschützte Endpoints nicht mehr mit `AllowAnonymous()` betreiben
- echte Auth-/Authorization-Konfiguration pro Endpoint-Gruppe oder Policy
- klare Trennung:
  - öffentlich
  - admin-auth
  - agent-auth
- Business-Checks bleiben im Handler, Primär-Auth nicht

### Ergebnis

- neue Endpoints können nicht versehentlich offen bleiben
- Sicherheitsgrenzen sind im Endpoint-Kontrakt sichtbar

### Validierung

- Admin-Endpunkte ohne Auth -> 401
- Agent-Endpunkte ohne Agent-Key -> 401
- falscher Boundary-Typ -> 401/403 gemäß Vertrag
- öffentliche Endpunkte bleiben erreichbar
- relevante Backend-Tests grün

## Phase S2 — Session Action Execution Split

### Ziel

`SessionActionService` von einem God Service in kleine, verständliche Ausführungspfade schneiden.

### Änderungen

- Legacy-/Dummy-Pfad und SSH-Pfad intern entkoppeln
- gemeinsame Session-Gates separat kapseln:
  - Session laden
  - Status/Expiry prüfen
  - Budget reservieren
- SSH-spezifische Policy-Resolution/Execution/Audit-Mapping aus dem zentralen Service ziehen

### Ergebnis

- dünner Orchestrator statt All-in-one-Service
- deutlich klarere Test- und Änderungsgrenzen

### Validierung

- Dummy-/Legacy-Happy-Path unverändert
- SSH-Happy-Path unverändert
- Budget-/Expiry-/Status-Verhalten unverändert korrekt
- `SessionActionService` sichtbar kleiner und einfacher

## Phase S3 — Audit Flow Typisieren

### Ziel

Den JSON-Blob-Kreisverkehr zwischen Application und Infrastructure beenden.

### Änderungen

- typed Audit-Payloads oder typed Event-Details einführen
- bounded projection aus typed Struktur statt Re-Parsing application-serialisierter JSON-Freiform
- Sanitizing-/Begrenzungsregeln beibehalten

### Ergebnis

- weniger implizite Layer-Verträge
- klarere Audit-Semantik
- stabilere Query-/Projection-Grenze

### Validierung

- Audit-Events enthalten weiterhin bounded Details
- keine Secret-/Output-Leak-Regression
- keine Pflicht mehr, application-serialisierte JSON-Details wieder zu parsen

## Phase S4 — Explizites SSH-Grant-/Profilmodell

### Ziel

SSH-Profile/Grants als echte Approval-/Session-Fachlichkeit modellieren.

### Änderungen

- Approval-/Session-Erzeugung modelliert explizite genehmigte SSH-Grants
- keine Prefix-Heuristik `ssh.` / `remote.` als Modellbasis
- keine `Targets.Count == 1`-Sonderregel als Fachregel

### Ergebnis

- SSH-Grants sind nachvollziehbar und explizit
- spätere Multi-Target-/Mixed-Request-Szenarien sind robuster

### Validierung

- Approval erzeugt nur explizit gültige Grants
- Non-SSH-/Mixed-/Multi-Target-Verhalten ist deterministisch
- Heuristik-Regeln sind als Architekturanker entfernt

## Phase S5 — DB-first Catalog Truthfulness

### Ziel

Den DB-first-Katalog im Code wirklich zum Single Source of Truth machen.

### Änderungen

- aktive Runtime-Policy-/Resolve-Pfade nur noch DB-backed
- kein aktiver Config-Runtime-Fallback als parallele Wahrheit
- Bootstrap/Seed bleibt Initialisierungs-/Import-Weg, nicht parallele Runtime-Autorität
- Approval-Validation und Runtime-Resolve sprechen dieselbe Wahrheit

### Ergebnis

- Entscheidung aus `docs/decisions.md` wird im Code wahr
- Phase 12.x kann auf sauberer Basis weitergeführt werden

### Validierung

- bekannte DB-Katalogkombinationen funktionieren
- deaktivierte/gelöschte DB-Einträge fail-closed
- Approval-Validation und Runtime-Resolve liefern dieselbe Wahrheit
- kein aktiver Alt-Truthfulness-Pfad mehr

## Phase S6 — Gezielt Ceremony abbauen

### Ziel

Überflüssige Monolith-Ceremony reduzieren, ohne die jetzt bereinigten Grenzen wieder zu verwischen.

### Änderungen

- Repository-/UnitOfWork-/DTO-Abstraktionen prüfen und nur dort abbauen, wo kein echter Boundary-Nutzen bleibt
- keine stilistische Großsanierung, sondern belegte Vereinfachung

### Ergebnis

- weniger Boilerplate
- weniger Layer-Hopping
- bessere AI-Navigierbarkeit

### Validierung

- bestehende Integrationstests grün
- keine neue Warnungen
- jede entfernte Abstraktion hat belegten Nicht-Nutzen

## Reihenfolgebegründung

1. S0 zuerst, weil der Working Tree aktuell unsauber ist.
2. S1 vor allem anderen, weil die Auth-Grenze die wichtigste Sicherheits- und Wartbarkeitsgrenze ist.
3. S2 vor S4/S5, weil der Session-Action-Kern zuerst entkoppelt werden muss.
4. S3 früh, weil der Audit-Fluss direkt im Ausführungskern hängt.
5. S4 vor bzw. mit S5, weil DB-first ohne explizites Grant-Modell fachlich weiter schief wäre.
6. S6 zuletzt, weil Ceremony-Abbau erst nach stabilen Grenzen sinnvoll ist.

## Global Validation Gates

Nach jeder umsetzenden Phase:

- `dotnet restore`
- `dotnet build`
- `dotnet test`
- `dotnet csharpier check .`
- relevante Integrations-/Boundary-Tests für auth/approval/session/action/audit
- `docker compose config`

Zusätzlich nach katalog-/truthfulness-relevanten Änderungen:

- minimaler End-to-End-Truthfulness-Smoke:
  - request
  - approve
  - session
  - action
  - audit

## Done Definition

Diese Sanierungsphase ist erst fertig, wenn:

- Auth framework-seitig sichtbar und erzwungen ist
- der Session-Action-Kern entkoppelt ist
- Audit nicht mehr über JSON-Kreisverkehr zwischen Layers lebt
- SSH-Grants explizit modelliert sind
- DB-first im Code kein Hybrid mehr ist
- die Doku den neuen Status korrekt wiedergibt

## Folge-Dokumente, die mitzupflegen sind

- `docs/implementation-plan.md`
- `docs/current-status.md`
- `docs/decisions.md`

## Nächster Schritt

Vor der ersten Implementierung:

1. offenen Working Tree sichern
2. sauberen Sanierungs-Branch schaffen
3. mit Phase S1 beginnen
