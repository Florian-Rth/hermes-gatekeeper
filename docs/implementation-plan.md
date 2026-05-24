# Hermes Gatekeeper — Multi-Phasen-Implementierungsplan

## Aktueller Implementierungsstand

Stand 2026-05-24: Der tatsächlich umgesetzte Schnitt ist weiter als die ursprüngliche Phasenreihenfolge in diesem Dokument. Dieses Dokument ist weiterhin verbindlich und muss vor jeder weiteren Entwicklungsphase gepflegt werden.

Abgeschlossen und auf `main` gepusht:

- Phase 0 Projektfoundation: `a59bb3e feat: scaffold gatekeeper full-stack foundation`
- Access Request Domain/Persistenz/API: `b041130 feat: add access request persistence and api`
- Approval/Deny + Sessions: `5fe72cf feat: add approval flow and sessions`
- Session Actions + Dummy Adapter: `7625807 feat: add session actions with dummy adapter`
- Minimal Approval Web UI: `35f2eec feat: add minimal approval web ui`
- Compose Admin-Token-Korrektur: `ed6cbec fix: pass admin token in compose`
- Phase 5 Session Lifecycle und Audit Controls Backend: `47502c8 feat: add session lifecycle and audit controls`
- Phase 6 Session Lifecycle und Audit Visibility UI: `8a3e345 feat: add session lifecycle audit ui`

Der aktuelle Backend-Kern kann:

```text
Access Request -> Approve/Deny in Web UI -> Session -> Execute typed dummy action -> Lifecycle controls in UI -> Audit browsing UI
```

Wichtige Abweichung vom ursprünglichen Plan:

- Approval + Sessions wurden backendseitig mit statischem Admin-Token umgesetzt, bevor eine vollständige lokale Admin-Login-UI existiert.
- Session Actions + Dummy Adapter wurden vor der Minimal Web UI umgesetzt, um den Produktkern früh per Integrationstests zu beweisen.
- Die Minimal Approval Web UI ist inzwischen umgesetzt und kann Requests listen, Details anzeigen, approve/deny ausführen, Session Summary anzeigen und safe Dummy Actions anstoßen.
- Session Lifecycle und Audit Visibility wurden backendseitig in Phase 5 umgesetzt und frontendseitig in Phase 6 sichtbar/bedienbar gemacht.
- Die ältere Phasennummerierung unten bleibt als strategischer Plan erhalten, ist aber nicht mehr exakt die Umsetzungsreihenfolge.

Für zukünftige Agents ist `docs/current-status.md` die führende Statusquelle für den Ist-Zustand. Dieses Dokument bleibt der verbindliche Roadmap-/Planungsrahmen und darf nicht ignoriert oder spontan ersetzt werden.

## Plan-Governance

Dieser Plan ist Teil des Entwicklungsprozesses, nicht nur Dokumentation nachträglich. Bei Arbeit an Hermes Gatekeeper gilt:

- Vor jeder Implementierungsphase diesen Plan und `docs/current-status.md` lesen.
- Keine Phase eigenmächtig überspringen, ersetzen oder „neu sortieren“, nur weil eine andere Reihenfolge kurzfristig attraktiver wirkt.
- Aufgabenpakete dürfen in eine andere Phase verschoben werden, wenn sie dort kontextuell besser passen oder technisch auf einer anderen Phase basieren.
- Jede Scope-Verschiebung muss vor der Implementierung dokumentiert werden: Was wird verschoben, wohin, warum, und welche Abhängigkeit entsteht dadurch?
- Grill-Me/Entscheidungscheck vor der Phase durchführen und Entscheidungen in das passende Phasendokument oder diesen Plan zurückschreiben.
- Implementierung erfolgt in kleinen, validierbaren Slices über frische Subagents; der Hauptagent bleibt Orchestrator.
- Nach jeder Phase `docs/current-status.md` und bei Roadmap-/Scope-Änderungen auch dieses Dokument aktualisieren.
- Der Plan darf angepasst werden; er darf nicht stillschweigend über den Haufen geworfen werden.

## Nächste konkrete Phase — Admin Authentication Hardening

Phase 6 hat die bestehenden Backend-Fähigkeiten für Session Lifecycle und Audit Visibility in der Web UI sichtbar und bedienbar gemacht. Die nächste konkrete Lücke ist nicht ein weiterer Adapter, sondern die Admin-Authentifizierung: Die UI nutzt weiterhin den manuell eingegebenen statischen Admin Token.

### Ziel

Den manuellen statischen Admin-Token-Eingabefluss durch eine kleine, lokale Admin-Authentifizierung ersetzen, ohne die expliziten Approval-Grenzen, Auditierbarkeit oder den frontend/backend-getrennten Workflow aufzugeben.

### Ergebnis am Ende der Phase

- Admins authentifizieren sich über einen dedizierten Login-/Session-Flow statt über ein dauerhaft sichtbares Token-Feld.
- Admin-geschützte UI-Aktionen funktionieren ohne manuelle Token-Wiederverwendung im Browser-State.
- Bestehende Approval-, Revoke- und Audit-Flows bleiben erhalten.
- Token/Session-Handling ist dokumentiert, getestet und auditierbar.

### Vorläufiger Scope

- Kleine lokale Admin-Auth evaluieren und planen.
- Login-/Logout-Flow und Session-Handling definieren.
- Backend und Frontend weiterhin in getrennte Slices/Phasen schneiden.
- Bestehende statische Token-Grenze nicht stillschweigend entfernen, bevor Ersatz validiert ist.

### Nicht-Ziele

- Keine produktiven Adapter.
- Keine freie Shell.
- Keine OIDC/Passkeys/TOTP/mTLS/multi-admin approval, außer Florian wählt diese Richtung explizit.
- Keine Policy DSL.
- Keine globale Session Operations Console, solange kein Session-List-API-Scope beschlossen ist.

### Validierung

Die konkrete Validierung wird im Phasen-Grill-Me festgelegt. Erwartet werden mindestens Backend-Integrationstests für Auth-Grenzen und Frontend-Tests für Login-/Logout-/geschützte Aktionen.

### Detailplan

Der konkrete Phasenplan liegt in `docs/phase-7-admin-authentication-hardening.md`.

Er legt fest:

- lokale Single-Admin-Auth statt sichtbarem Admin-Token-Feld.
- HttpOnly Cookie-Session für Browser-Adminzugriff.
- neue Endpunkte `POST /api/v1/admin/login`, `POST /api/v1/admin/logout`, `GET /api/v1/admin/me`.
- Migration von approve/deny/revoke/audit auf die neue Admin-Session-Grenze.
- keine stille Änderung am `complete`-Endpoint.
- Backend- und Frontend-Slices bleiben getrennt.

### Commit Boundary

Diese Phase darf implementiert werden, nachdem der Detailplan in `docs/phase-7-admin-authentication-hardening.md` als aktiver Phasenplan gelesen wurde. Implementierung erfolgt in getrennten Backend-/Frontend-Slices über frische Subagents.

---

## Ziel

Dieser Plan beschreibt, in welchen Phasen Hermes Gatekeeper vom leeren Repository zu einem brauchbaren, selbsthostbaren MVP und danach zu einem allgemeineren Open-Source-Produkt ausgebaut werden soll.

Der Plan ist bewusst phasenorientiert: Jede Phase hat ein klares Ergebnis, eine Abbruchkante und eine sinnvolle Demo. Detail-Tickets und konkrete Code-Tasks können später aus den Phasen abgeleitet werden.

## Leitplanken

- HTTP API first: Die API ist das Kernprodukt, Hermes-Toolset/MCP/CLI sind spätere Adapter.
- Generischer Kern: Kein Home-Assistant-first Design im Core.
- Broker/Proxy-first: Hermes bekommt keine Produktiv-Credentials.
- Typisierte Actions statt freier Shell.
- Read-only und Dummy/Test-Adapter zuerst.
- Auditierbarkeit ist kein Add-on, sondern Teil des MVP.
- Docker Compose ist das MVP-Deployment-Ziel.
- SQLite reicht für den MVP; Postgres bleibt später optional.
- Lokale Admin-Auth reicht für den MVP; OIDC/Passkeys/mTLS kommen später.
- Repo-Struktur ist verbindlich: Backend in `backend/`, Frontend in `frontend/`.
- Backend nutzt EF Core + SQLite + Migrations ab Phase 1.
- MVP-Auth nutzt ENV-seeded lokalen Admin und statischen Agent Client Token.
- Frontend nutzt React + Vite + MUI + TanStack Query + Zod + Biome + Vitest.

## Arbeitsworkflow

Die Umsetzung folgt Florians AI-Coding-Workflow:

1. Grobe Idee mit Grill-Me ausarbeiten.
2. Markdown-Masterplan erstellen oder aktualisieren.
3. Pro Phase einen frischen Agenten/Kontext starten.
4. Die Phase mit Grill-Me ausarbeiten.
5. Entscheidungen und Scope-Verschiebungen zurück in diesen Plan schreiben.
6. Phase in kleinen validierbaren Slices implementieren.
7. Relevante Validierung ausführen.
8. Committen und pushen.
9. Für die nächste Phase wieder frischen Kontext verwenden.

Backend-Arbeit folgt `florian-backend-work` und `florian-tdd`. Frontend-Arbeit folgt `florian-frontend-work`. Codex darf als Implementierungsworker eingesetzt werden, aber nicht ohne anschließenden Spec-/Quality-Review.

---

## Phase 0 — Projektgrundlage und Entwicklungsrahmen

### Ziel

Das Repository wird so vorbereitet, dass Backend, Frontend, Tests, lokale Entwicklung und Docker-Deployment sauber wachsen können.

### Ergebnis am Ende der Phase

Ein leerer, aber lauffähiger Projekt-Skeleton existiert:

- .NET Backend-Projekt mit ASP.NET Core und FastEndpoints
- React/Vite Frontend-Projekt mit MUI, TanStack Query, Zod, Biome und Vitest
- gemeinsame Docker-Compose-Entwicklung
- Basis-Konfiguration für SQLite
- automatisierte Tests laufen lokal
- README erklärt lokale Entwicklung

### Inhalte

- Solution-Struktur in `backend/` anlegen, z.B.:
  - `backend/Gatekeeper.sln`
  - `backend/src/Gatekeeper.Api`
  - `backend/src/Gatekeeper.Core`
  - `backend/src/Gatekeeper.Application`
  - `backend/src/Gatekeeper.Infrastructure`
  - `backend/tests/Gatekeeper.Tests`
- Frontend-Struktur in `frontend/` anlegen.
- FastEndpoints einrichten.
- OpenAPI/Swagger aktivieren.
- Health Endpoint hinzufügen.
- SQLite-Konfiguration vorbereiten.
- Testframework einrichten.
- Dockerfile und Docker Compose Skeleton anlegen.
- grundlegende CI optional vorbereiten.

### Nicht-Ziel

- noch keine echte Auth
- noch kein Request-/Session-Modell
- noch keine UI außer maximal Platzhalter

### Demo

- `docker compose up` startet Backend und Frontend.
- Swagger/OpenAPI ist erreichbar.
- Health Endpoint antwortet erfolgreich.

---

## Phase 1 — Domain-Modell und Persistenz

### Ziel

Der fachliche Kern wird als sauberes Domain-Modell definiert, bevor API und UI zu viel Verhalten festzementieren.

### Ergebnis am Ende der Phase

Die Kernobjekte existieren im Code, können validiert und in SQLite gespeichert werden.

### Inhalte

Domain-Modelle:

- `AccessRequest`
- `AccessRequestStatus`
- `Session`
- `SessionStatus`
- `Capability`
- `Target`
- `ActionRequest`
- `ActionResult`
- `AuditEvent`
- `AgentClient`
- `AdminUser`

Persistenz:

- SQLite Schema/Migrations
- Repositories oder DbContext
- Basistests für Erstellen, Laden, Statuswechsel

Validierung:

- Pflichtfelder
- erlaubte Statusübergänge
- Laufzeitgrenzen
- Capability-/Target-Struktur
- sichere Defaults

### Nicht-Ziel

- noch kein vollständiger Approval Flow
- noch kein Action Broker
- noch keine produktiven Adapter

### Demo

- Tests beweisen, dass Requests, Sessions und Audit Events persistiert werden können.
- Statusübergänge wie pending → approved/denied und active → completed/revoked sind abgesichert.

---

## Phase 2 — Access Request API

### Ziel

Agenten können strukturierte Zugriffsanfragen über eine versionierte HTTP API erstellen und abfragen.

### Ergebnis am Ende der Phase

Ein Agent kann eine Access Request erstellen, den Status lesen und die Liste eigener Requests abrufen.

### API-Scope

- `POST /api/v1/access-requests`
- `GET /api/v1/access-requests/{id}`
- `GET /api/v1/access-requests`

### Inhalte

- Request-/Response-DTOs definieren.
- FastEndpoints-Endpunkte implementieren.
- Validierung integrieren.
- Audit Event `AccessRequestCreated` schreiben.
- OpenAPI-Dokumentation prüfen.
- Basis-Agent-Auth vorbereiten, zunächst z.B. statischer Client Token.

### Sicherheitsziel

Der Agent darf Requests erstellen und eigene Requests lesen, aber nichts genehmigen, löschen oder an Policies ändern.

### Nicht-Ziel

- noch keine Admin UI
- noch kein Approve/Deny
- noch keine Actions

### Demo

Per HTTP/curl:

1. Request erstellen.
2. Response enthält `id`, `status: pending`, optional `approvalUrl`.
3. Request kann per ID gelesen werden.
4. Audit Log enthält Erstellungsereignis.

---

## Phase 3 — Lokale Admin-Auth und Approval API

### Ziel

Ein lokaler Admin kann sich anmelden und Access Requests genehmigen oder ablehnen.

### Ergebnis am Ende der Phase

Der Approval Flow existiert backendseitig vollständig. Eine Genehmigung erzeugt eine Session.

### API-Scope

- Admin Login/Logout oder Cookie-/Token-basierte lokale Session
- `POST /api/v1/access-requests/{id}/approve`
- `POST /api/v1/access-requests/{id}/deny`
- `GET /api/v1/sessions/{id}`

### Inhalte

- lokales Admin-Modell implementieren
- initiales Admin-Setup definieren, z.B. Setup Secret oder initialer Seed über Environment Variable
- Passwort-Hashing sauber umsetzen
- Admin Session/Cookie einrichten
- Approve/Deny-Endpunkte implementieren
- bei Approve Session erzeugen
- Audit Events schreiben:
  - `AdminLoginSucceeded`
  - `AdminLoginFailed`
  - `AccessRequestApproved`
  - `AccessRequestDenied`
  - `SessionCreated`

### Sicherheitsziel

Approval ist klar vom Agent-Token getrennt. Ein Agent-Token darf niemals Admin-Endpunkte nutzen.

### Nicht-Ziel

- noch keine schöne Web UI
- noch keine echten Actions
- noch keine TOTP/OIDC/Passkeys

### Demo

1. Agent erstellt Request.
2. Admin authentifiziert sich.
3. Admin approved Request.
4. Request wird `approved`.
5. Session wird erzeugt.
6. Audit Log zeigt vollständige Kette.

---

## Phase 4 — Minimal Web UI für Approval

### Ziel

Der menschliche Genehmigungsprozess wird benutzbar. Requests sollen verständlich dargestellt werden, nicht als rohe JSON-Wand.

### Ergebnis am Ende der Phase

Eine einfache React Web UI erlaubt Login, Request-Liste, Detailansicht, Approve, Deny und Session-Übersicht.

### UI-Scope

- Login-Seite
- Liste pending Requests
- Request-Detailseite
- Approve/Deny mit Kommentar
- Liste aktiver Sessions
- Session-Detailansicht
- Revoke-Button, falls Backend schon vorhanden oder in Phase 6 nachziehen

### Inhalte

- Frontend Routing
- API Client für Backend
- Auth-State im Frontend
- menschenlesbare Darstellung von:
  - Intent
  - Requester
  - Targets
  - Requested Capabilities
  - Duration
  - Risk
  - Justification
  - Proposed Actions
  - Forbidden Actions
- klare visuelle Unterscheidung von low/medium/high Risk
- Fehler- und Loading-States

### Nicht-Ziel

- kein Design-Finish
- keine Policy-Bearbeitung
- keine Push Notifications

### Demo

Der komplette Mensch-in-der-Schleife-Flow funktioniert im Browser:

1. Agent erstellt Request.
2. Admin sieht Request in UI.
3. Admin öffnet Detailseite.
4. Admin approved oder denied.
5. Agent sieht geänderten Status über API.

---

## Phase 5 — Policy Engine MVP

### Ziel

Genehmigte Sessions und Actions werden nicht nur gespeichert, sondern aktiv gegen Regeln geprüft.

### Ergebnis am Ende der Phase

Eine minimale Policy Engine entscheidet deterministisch, ob Request/Session/Action erlaubt ist.

### Regeln im MVP

- Session muss aktiv sein.
- Session darf nicht abgelaufen sein.
- Session darf nicht revoked/completed sein.
- `maxActions` darf nicht überschritten sein.
- Action Capability muss genehmigt sein.
- Target muss genehmigt sein.
- Action Type muss bekannt sein.
- write/high-risk Actions sind im Dummy-MVP blockiert, außer explizit erlaubt.

### Inhalte

- Policy Service abstrahieren.
- `PolicyDecision` modellieren:
  - allowed/denied
  - reason code
  - human-readable reason
- Tests für alle Deny-Fälle.
- Audit Event `ActionDenied` vorbereiten.

### Nicht-Ziel

- keine komplexe Policy DSL
- keine UI zum Policy-Bearbeiten
- keine automatische Genehmigung

### Demo

Tests zeigen:

- erlaubte Action wird zugelassen
- falsches Target wird blockiert
- falsche Capability wird blockiert
- abgelaufene Session wird blockiert
- überschrittenes Action-Limit wird blockiert

---

## Phase 6 — Dummy/Test Action Broker

### Ziel

Der komplette End-to-End-Flow wird ausführbar, ohne echte Zielsysteme anzufassen.

### Ergebnis am Ende der Phase

Ein Agent kann nach Approval typisierte Dummy Actions ausführen. Gatekeeper prüft Policy, führt Dummy Adapter aus und auditiert alles.

### API-Scope

- `POST /api/v1/sessions/{id}/actions`
- `POST /api/v1/sessions/{id}/complete`
- `POST /api/v1/sessions/{id}/revoke`

### Dummy Actions

- `test.echo`
- `test.status.read`
- `test.logs.read`

### Inhalte

- Action Broker Interface definieren.
- Dummy Adapter implementieren.
- Policy Check vor jeder Action erzwingen.
- Action Counter erhöhen.
- Outputs begrenzen.
- Audit Events schreiben:
  - `ActionExecuted`
  - `ActionDenied`
  - `SessionCompleted`
  - `SessionRevoked`
  - `SessionExpired`
- Session Expiry behandeln, z.B. lazy beim Zugriff oder per Background Job.

### Sicherheitsziel

Auch der Dummy Adapter darf nicht um die Policy Engine herum ausführbar sein. Alle Actions laufen durch denselben Broker-Pfad wie spätere echte Adapter.

### Demo

Kompletter MVP-Kern per API:

1. Agent erstellt Request für `test.logs.read`.
2. Admin approved in UI.
3. Agent führt `test.logs.read` aus.
4. Gatekeeper liefert Dummy Logs.
5. Audit Log enthält alle Ereignisse.
6. Agent completed Session.

---

## Phase 7 — Audit UI und Betriebsreife für MVP

### Ziel

Der MVP wird nachvollziehbar, debugbar und lokal betreibbar.

### Ergebnis am Ende der Phase

Admins können Audit Events ansehen. Docker Compose ist dokumentiert. Der MVP kann als lokale Demo und internes Testsystem laufen.

### Inhalte

- Audit-API ergänzen:
  - Liste Audit Events
  - Filter nach Request ID / Session ID / Event Type
- Audit-Ansicht in der Web UI
- strukturierte Logs im Backend
- Konfigurationsdokumentation
- Docker Compose finalisieren
- `.env.example`
- Backup-/Datenbank-Hinweise für SQLite
- Fehlerseiten und sinnvolle API-Fehlerantworten
- grundlegende Security Headers / CORS-Konfiguration

### Nicht-Ziel

- noch keine manipulationssichere Hash Chain
- noch kein Loki/ELK Export
- noch keine produktiven Zielsystemadapter

### Demo

Ein neuer Nutzer kann anhand der README lokal starten, Request genehmigen, Dummy Action ausführen und danach im Audit nachvollziehen, was passiert ist.

---

## Phase 8 — MVP Hardening und Release-Kandidat

### Ziel

Aus dem funktionierenden MVP wird ein sauberer erster Release-Kandidat für Open Source.

### Ergebnis am Ende der Phase

Der MVP ist testbar, dokumentiert, versioniert und als erstes Release markierbar.

### Inhalte

- Integrationstests für kompletten Flow
- API Contract Tests gegen OpenAPI
- negative Security Tests:
  - Agent kann nicht approven
  - unauthentifizierter Zugriff blockiert
  - falsche Session blockiert
  - revoked/expired Sessions blockieren Actions
- einfache Rate Limits prüfen
- sensible Daten in Logs vermeiden
- README für Installation und Demo aktualisieren
- Architekturdiagramm optional ergänzen
- Lizenz festlegen
- Contribution-Hinweise vorbereiten
- Release Notes schreiben

### Release-Kriterium

Der Release-Kandidat ist erreicht, wenn eine frische Docker-Compose-Installation ohne manuelle Code-Schritte den vollständigen Dummy-End-to-End-Flow ausführen kann.

### Demo

`docker compose up` plus dokumentierte curl-Kommandos und Web UI reichen aus, um Gatekeeper komplett zu demonstrieren.

---

# Nach-MVP-Phasen

## Phase 9 — Generischer HTTP Read-only Adapter

### Ziel

Der erste echte, aber risikoarme Adapter wird gebaut: HTTP read-only gegen konfigurierte Targets.

### Ergebnis

Gatekeeper kann erlaubte HTTP GET/HEAD Requests gegen whitelisted Endpunkte ausführen.

### Inhalte

- Target-Konfiguration für HTTP Services
- Allowlist für Hosts, Pfade, Methoden
- nur GET/HEAD im ersten Schritt
- Timeout, Max Response Size, Content-Type Filter
- Secrets nicht an Agent ausgeben
- Audit mit URL, Status, Dauer, gekürztem Output

### Nutzen

Guter erster Realitätscheck ohne SSH oder Systemzugriffe.

---

## Phase 10 — SSH Read-only Adapter für Test-VM

### Ziel

Gatekeeper lernt read-only Observability gegen eine unkritische Test-VM.

### Ergebnis

Typisierte SSH-basierte Actions funktionieren auf erlaubten Test-Targets.

### Beispiel-Actions

- `system.status.read`
- `logs.read`
- `service.status.read`
- `disk.usage.read`
- `memory.status.read`

### Sicherheitsrahmen

- keine freie Shell als Standard
- nur vordefinierte Kommandos
- kein sudo im ersten Schritt
- feste Target-Allowlist
- Output-Limits
- Timeout pro Action

---

## Phase 11 — Docker Read-only Adapter

### Ziel

Container-Observability wird als typisierte Actions verfügbar.

### Ergebnis

Gatekeeper kann erlaubte Docker-Informationen lesen, ohne Schreibrechte zu vergeben.

### Beispiel-Actions

- `container.list`
- `container.inspect.read`
- `container.logs.read`
- `container.stats.read`

### Sicherheitsrahmen

- kein Container Start/Stop/Restart
- keine Exec-Shell
- keine Secrets aus Env ausgeben oder Ausgabe filtern

---

## Phase 12 — Safe Write Actions

### Ziel

Erste kontrollierte Schreibaktionen werden möglich, aber nur stark typisiert und explizit genehmigt.

### Ergebnis

Gatekeeper kann ausgewählte Low-/Medium-Risk Maintenance Actions ausführen.

### Beispiel-Actions

- `service.restart`
- `container.restart`
- `backup.trigger`

### Zusätzliche Anforderungen

- höhere Risk-Klasse
- explizite Anzeige in UI
- separate Bestätigungstexte
- optional kürzere Session-Laufzeit
- klare Rollback-/Recovery-Hinweise in der UI

---

## Phase 13 — Home Assistant Adapter

### Ziel

Home Assistant wird als konkreter Adapter ergänzt, ohne den generischen Core zu verbiegen.

### Ergebnis

Gatekeeper kann Home-Assistant-Zustände und Logs lesen und später kontrollierte Service Calls ausführen.

### Reihenfolge

1. read-only:
   - states lesen
   - entity history lesen, falls verfügbar
   - logs lesen, falls sicher möglich
2. safe writes:
   - erlaubte Service Calls auf erlaubte Domains/Entities
3. advanced:
   - Automationen lesen/ändern nur nach separatem Design

### Sicherheitsrahmen

- HA Token liegt nur bei Gatekeeper, nie bei Hermes.
- Entity-/Domain-Allowlist.
- keine freien Template-/YAML-Änderungen im ersten Schritt.

---

## Phase 14 — Bessere Identity und Transport-Sicherheit

### Ziel

Der MVP-Auth-Ansatz wird für ernsthaftere Deployments erweitert.

### Ergebnis

Gatekeeper kann in typische Self-Hosting-Identity-Setups integriert werden.

### Optionen

- OIDC für Admin Login
- Passkeys/WebAuthn
- TOTP für lokale Admins
- mTLS zwischen Agent und Gatekeeper
- rotierbare Agent Client Tokens
- Recovery Codes

### Wichtig

Diese Phase darf den MVP nicht komplizieren. Lokale Auth bleibt für einfache Installationen nutzbar.

---

## Phase 15 — Adapter-Ökosystem und Agent-Integrationen

### Ziel

Gatekeeper wird einfacher in verschiedene Agenten- und Automationssysteme integrierbar.

### Ergebnis

Neben HTTP API entstehen dünne Adapter, ohne den Core zu duplizieren.

### Mögliche Artefakte

- CLI:
  - `gatekeeper request ...`
  - `gatekeeper session action ...`
- Hermes Toolset als Thin Wrapper
- MCP Server
- kleine Client SDKs

### Leitregel

Alle Adapter sprechen intern die öffentliche HTTP API. Es gibt keinen zweiten Security-Core.

---

## Phase 16 — Audit-Hardening und Enterprise-nahe Features

### Ziel

Audit und Betrieb werden robuster für längere Nutzung und mehrere Nutzer.

### Ergebnis

Gatekeeper ist besser nachvollziehbar, exportierbar und manipulationsresistenter.

### Inhalte

- append-only JSONL zusätzlich zur DB
- Hash Chain für Audit Events
- Export nach Loki/ELK/Grafana
- Retention Policies
- Audit Backups
- Multi-Admin Approval
- Break-glass Sessions mit strenger Auditierung
- optional Session Recording für riskantere Adapter

---

## Empfohlene erste Umsetzungsschritte

Für die konkrete Implementierung sollten zuerst nur Phase 0 bis Phase 8 umgesetzt werden. Das ist der MVP.

Danach ist die nächste sinnvolle Reihenfolge:

1. Phase 9 — HTTP read-only Adapter
2. Phase 10 — SSH read-only Adapter für Test-VM
3. Phase 11 — Docker read-only Adapter
4. Phase 12 — Safe Write Actions
5. Phase 13 — Home Assistant Adapter

## MVP Definition of Done

Der MVP ist fertig, wenn:

- Gatekeeper per Docker Compose startet.
- Admin-Login funktioniert.
- Agent kann Access Request per HTTP erstellen.
- Admin kann Request in Web UI sehen und genehmigen/ablehnen.
- Approval erzeugt begrenzte Session.
- Agent kann erlaubte Dummy Actions ausführen.
- Policy Engine blockiert nicht genehmigte Actions.
- Sessions können completed, revoked und expired sein.
- Audit Log enthält alle relevanten Events.
- OpenAPI dokumentiert die Agent-/Admin-API.
- README erklärt lokale Installation und Demo-Flow.

## Nicht vor dem MVP bauen

- Home Assistant Adapter
- SSH gegen produktive Hosts
- freie Shell
- Proxmox Adapter
- OIDC
- Passkeys
- Multi-Admin Approval
- MCP Server
- Hermes Toolset
- Kubernetes/Helm
- komplexe Policy DSL

Diese Dinge sind wertvoll, aber sie würden das Kernmodell zu früh aufblasen.
