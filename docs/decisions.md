# Hermes Gatekeeper — Entscheidungen und offene Fragen

## Getroffene Entscheidungen

### 2026-05-22 — Projektname

Name: **Hermes Gatekeeper**

Begründung: Der Name ist direkt, verständlich und macht klar, dass es um kontrollierte Zugriffe für Hermes geht.

### 2026-05-22 — Hermes soll mächtig bleiben

Entscheidung: Hermes wird nicht künstlich auf minimale Profile reduziert. Florian möchte zukünftig auch per WhatsApp komplexe Aufgaben an Hermes geben können.

Konsequenz: Sicherheit soll nicht primär durch dauerhaft deaktivierte Fähigkeiten entstehen, sondern durch kontrollierte, explizit genehmigte Zugriffswege auf produktive Systeme.

### 2026-05-22 — Kein Standardzugriff auf externe produktive Systeme

Entscheidung: Hermes hat standardmäßig keinen Zugriff auf produktive Systeme außerhalb seiner VM.

Konsequenz: Andere VMs, anderer Server, Home Assistant, Proxmox etc. werden über Gatekeeper angebunden.

### 2026-05-22 — Externer Genehmigungsdienst

Entscheidung: Es soll eine separate Genehmigungs-App/API auf einer anderen VM per Docker laufen.

Konsequenz: Gatekeeper ist eine eigene Sicherheitsgrenze zwischen Hermes und Produktivsystemen.

### 2026-05-22 — Web-basierte Genehmigung

Entscheidung: Genehmigungsanfragen werden per Web-View bestätigt oder abgelehnt.

Konsequenz: Requests müssen für Menschen lesbar aufbereitet werden und dürfen nicht nur rohe JSON-Payloads sein.

### 2026-05-22 — Zeit- und/oder aufgabenbegrenzte Sessions

Entscheidung: Genehmigte Zugriffe öffnen eine begrenzte Session.

Mögliche Grenzen:

- Zeitlimit
- Anzahl Aktionen
- Zielsysteme
- Capability-Scope
- manueller Widerruf
- Abschluss durch Hermes

### 2026-05-22 — Broker/Proxy-first Architektur bevorzugt

Entscheidung: Der bevorzugte Standard ist, dass Hermes produktive Systeme nicht direkt anspricht. Stattdessen führt Gatekeeper genehmigte Aktionen als Broker/Proxy aus.

Konsequenz: Hermes bekommt keine echten Produktiv-Credentials. Direkter SSH-/VPN-Zugriff bleibt optionaler Break-glass-/Advanced-Modus.

### 2026-05-22 — Typisierte Aktionen statt freie Shell

Entscheidung: Der sichere Standard sind typisierte Operationen wie `read_service_logs` statt freie Shell-Kommandos.

Konsequenz: Gatekeeper kann viel genauer prüfen, was erlaubt ist. Freie Shell wird nur optional, stark begrenzt und gesondert genehmigt.

### 2026-05-22 — Open-Source-Produkt statt reines Privatprojekt

Entscheidung: Hermes Gatekeeper wird als allgemeines, selbsthostbares Open-Source-Projekt gedacht. Florians HomeLab ist der erste reale Use Case, aber nicht die Produktgrenze.

Konsequenz: Architektur, API, Dokumentation und Deployment sollen generisch und für andere Nutzer nachvollziehbar sein.

### 2026-05-22 — Generischer Kern, nicht Home-Assistant-first

Entscheidung: Das erste technische Ziel ist nicht Home Assistant, sondern ein generisches Target-/Capability-/Session-Modell.

Konsequenz: Home Assistant wird später ein Adapter wie SSH, Docker oder Proxmox. Der MVP soll keine HA-spezifischen Grundannahmen im Kernmodell haben.

### 2026-05-22 — Tech Stack: .NET + FastEndpoints

Entscheidung: Backend wird mit dem neuesten .NET / ASP.NET Core und FastEndpoints gebaut.

Konsequenz: API-first Entwicklung mit sauberer OpenAPI/Swagger-Dokumentation. FastEndpoints-Struktur soll für modulare Features/Vertical Slices genutzt werden.

### 2026-05-22 — Lokale Admin-Auth im MVP

Entscheidung: Auth startet mit lokaler Admin-Auth.

Konsequenz: Kein OIDC/Authentik/Authelia im MVP erforderlich. OIDC, Passkeys/WebAuthn und externe Identity Provider bleiben spätere Erweiterungen.

### 2026-05-22 — Hermes Interface: HTTP API first

Entscheidung: Das primäre Interface für Hermes ist eine HTTP API. Ein Hermes Toolset ist optionaler späterer Adapter.

Konsequenz: Gatekeeper bleibt generisch und nicht Hermes-only. Andere Agenten, CLIs und Scripts können dieselbe API nutzen. Details stehen in `docs/interface-model.md`.

### 2026-05-22 — Repo-Struktur: backend/ und frontend/

Entscheidung: Das Repository enthält Backend und Frontend. Backend-Code liegt unter `backend/`, Frontend-Code unter `frontend/`.

Konsequenz: Alle Pläne, Agentenprompts, Docker-Dateien und Validierungsbefehle müssen diese Struktur verwenden. Alte Beispielpfade wie `src/...` oder `web/` sind nicht mehr gültig.

### 2026-05-22 — MVP Auth: ENV Admin + statischer Agent Token

Entscheidung: Der MVP nutzt einen lokalen Admin, der beim Start aus Environment-Variablen angelegt wird, sowie einen statischen Agent Client Token aus Environment-Konfiguration.

Konsequenz: Kein Setup-Flow im MVP. Admin-Auth und Agent-Auth sind trotzdem klar getrennt. Docker Compose kann `.env.example` dokumentieren.

### 2026-05-22 — Frontend Stack: React/Vite/MUI/TanStack Query/Zod/Biome/Vitest

Entscheidung: Das Frontend wird mit React + Vite gebaut und nutzt MUI, TanStack Query, Zod, Biome und Vitest.

Konsequenz: Die Frontend-Implementierung folgt den Regeln aus `florian-frontend-work`: feature-basierte Module, validierte API-Boundaries, MUI-Konventionen, Biome statt ESLint/Prettier.

### 2026-05-22 — Persistenz: EF Core + SQLite + Migrations ab Phase 1

Entscheidung: Der MVP nutzt EF Core mit SQLite und Migrations ab der Domain-/Persistenzphase.

Konsequenz: Der MVP baut nicht erst auf In-Memory-Repositories. Domain-, Service- und Persistenzgrenzen werden früh testbar und realitätsnah geschnitten.

## Vorläufige technische Präferenzen

Aktueller Stand:

- Backend: neuestes .NET / ASP.NET Core
- API Framework: FastEndpoints
- Frontend: React
- Frontend Details: Vite, MUI, TanStack Query, Zod, Biome, Vitest
- Deployment: Docker Compose für MVP
- Datenbank: EF Core + SQLite + Migrations für MVP, Postgres später möglich
- Auth MVP: lokale Admin-Auth per ENV Admin Seed
- Transport Hermes -> Gatekeeper: Client Token für MVP; mTLS später prüfen
- Audit: DB + JSONL denkbar, später append-only Hash Chain

## Offene Fragen

### Auth / Identity

- Wie sollen ENV Admin Credentials in der Produktion rotiert werden?
- Soll es nach dem MVP einen Setup-Flow statt ENV-Seed geben?
- Sollen Recovery Codes oder TOTP nach dem MVP ergänzt werden?
- Soll die Web-UI nur intern erreichbar sein oder auch extern/VPN?
- Soll WhatsApp-Genehmigung später zusätzlich möglich sein oder ausschließlich Web-UI?
- Welche OIDC-Lösung wäre später optional sinnvoll, falls Nutzer bereits Authentik/Authelia/Keycloak einsetzen?

### Netzwerkmodell

- Kann Hermes VM Gatekeeper erreichen?
- Kann Gatekeeper alle Zielsysteme erreichen?
- Soll Gatekeeper in einem separaten Management-Netz hängen?
- Soll mTLS zwischen Hermes und Gatekeeper eingesetzt werden?

### Target-Systeme MVP

Das MVP soll nicht Home-Assistant-first sein. Erstes technisches Ziel ist ein generischer Target-/Capability-/Session-Flow mit Dummy- oder Test-Adapter.

Kandidaten:

1. Dummy/Test Adapter für vollständigen End-to-End Flow
2. generischer HTTP Target Adapter read-only
3. generischer SSH read-only Adapter auf unkritische Test-VM
4. später: Docker, Home Assistant, Proxmox als konkrete Adapter

### Policy-Modell

- YAML-Konfig oder DB-basiert?
- Policies versioniert in Git?
- UI zum Bearbeiten von Policies oder nur manuelle Config?
- Wie detailliert sollen Service-/Container-Allowlists sein?

### Audit-Aufbewahrung

- Wie lange Logs aufbewahren?
- Sollen Outputs gespeichert werden oder nur Hashes/Summaries?
- Wohin mit Backups?
- Muss Audit manipulationssicher sein?

### Integration in Hermes

- HTTP API ist für MVP entschieden.
- Wie soll Hermes sich authentifizieren?
  - statischer Client Token?
  - mTLS?
  - signierte Request-Payloads?
- Soll später zusätzlich ein Hermes Toolset gebaut werden?
- Soll Gatekeeper später als MCP Server angeboten werden?

### Risiko-Klassifikation

- Wie unterscheiden wir low/medium/high/break-glass?
- Welche Aktionen sind immer high-risk?
- Welche Aktionen dürfen niemals automatisiert genehmigt werden?

## Nächste Schritte

1. MVP-Scope festlegen: Dummy/Test Adapter vs. generischer HTTP Adapter vs. SSH read-only Test-Adapter.
2. Domain-Modell definieren: AccessRequest, Session, Capability, Target, Action, AuditEvent.
3. API-Kontrakt als OpenAPI-first Entwurf festhalten.
4. Konkreten Implementierungsplan für .NET + FastEndpoints schreiben.
