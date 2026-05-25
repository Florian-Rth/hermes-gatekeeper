# Hermes Gatekeeper — MVP Scope Draft

## Aktueller MVP-Stand

Stand 2026-05-25 ist der Dummy-MVP-Kern bis Admin Login, Approval, Session Lifecycle, Audit UI und Dummy-Action-Broker umgesetzt:

```text
Access Request -> Admin Login -> Approve/Deny -> Session -> Execute typed dummy action -> Lifecycle Controls -> Audit UI
```

Bereits implementiert:

- Access Requests erstellen, lesen und listen.
- Lokale Admin-Login-/Cookie-Session statt sichtbarem Admin Token.
- Session-Erzeugung bei Approval.
- Session Details, Revoke, Complete, Action Budget und Audit UI.
- Session Action Execution über `POST /api/v1/sessions/{sessionId}/actions`.
- Dummy Capabilities `test.echo`, `test.status.read`, `test.fail`.
- Audit Events für Request, Admin Login/Logout, Approval/Deny, Session und Action-Flows.
- HTTP-Integrationstests für Happy Path und zentrale Fehlerfälle.

Noch offen für den MVP:

- generischer SSH read-only Connector als erster echter, breit anwendbarer Target Connector.
- persistente Compose-Daten/Keys für lauffähigen MVP-Betrieb.
- erste dokumentierte End-to-End-Demo über Docker Compose mit Dummy- und SSH-read-only-Flow.

Details stehen in `docs/current-status.md`.

## Ziel des MVP

Der MVP soll den generischen Kern von Hermes Gatekeeper beweisen:

1. Ein Agent kann eine strukturierte Access Request per HTTP API erstellen.
2. Ein Admin kann die Anfrage über lokale Web-UI/Admin-Auth genehmigen oder ablehnen.
3. Bei Genehmigung entsteht eine begrenzte Session.
4. Der Agent kann innerhalb der Session nur erlaubte Actions ausführen.
5. Mindestens ein echter generischer Connector beweist die Zielsystem-Anbindung in minimaler, sicherer Form.
6. Alle Schritte werden auditiert.
7. Das System ist per Docker Compose selbsthostbar.

Nicht Ziel des MVP: spezielle HomeLab-Integrationen wie Home Assistant, Docker, Proxmox oder freie Shell. Ziel des MVP ist aber ein generischer SSH-read-only Connector, weil SSH systemübergreifend einsetzbar ist und das Endziel minimal real beweist.

## Tech Stack

- .NET / ASP.NET Core, neueste stabile Version
- FastEndpoints
- React + Vite
- MUI
- TanStack Query
- Zod
- Biome
- Vitest
- EF Core + SQLite + Migrations
- Docker Compose
- lokale Admin-Auth per ENV-seeded Admin
- statischer Agent Client Token per ENV
- OpenAPI/Swagger

## MVP-Leitentscheidung

Der MVP nutzt zwei Connector-Stufen:

1. Dummy/Test Adapter für risikofreie End-to-End-Tests.
2. Generischer SSH-read-only Connector als erster echter, breit anwendbarer Target Connector.

Begründung:

- Dummy beweist das Produktmodell ohne produktive Risiken.
- SSH-read-only beweist das eigentliche Endziel in minimaler, real lauffähiger Form.
- Der Kern bleibt generisch und wird nicht Home-Assistant-first.
- Spezielle Connectoren bleiben Post-MVP, aber SSH ist als generischer Systemzugriff Open-Source-tauglich und für sehr viele Umgebungen verwendbar.
- Typisierte SSH-Actions erlauben strenge Kontrolle ohne freie Shell.

## MVP Features

## Repo-Struktur

- Backend: `backend/`
- Frontend: `frontend/`
- Dokumentation: `docs/`
- Docker Compose auf Repo-Root-Ebene

Backend-Solution:

- `backend/Gatekeeper.sln`
- `backend/src/Gatekeeper.Api`
- `backend/src/Gatekeeper.Core`
- `backend/src/Gatekeeper.Application`
- `backend/src/Gatekeeper.Infrastructure`
- `backend/tests/Gatekeeper.Tests`

Frontend-App:

- `frontend/`
- Feature-basierte Module unter `frontend/src/features/<name>/`
- Shared UI/Layout unter `frontend/src/components`, `frontend/src/lib`, `frontend/src/styles`

### 1. Access Requests

API:

- `POST /api/v1/access-requests`
- `GET /api/v1/access-requests/{id}`
- `GET /api/v1/access-requests`

Felder:

- intent
- requester
- targets
- requestedCapabilities
- durationMinutes
- risk
- justification
- proposedActions
- forbiddenActions
- metadata

### 2. Admin Approval UI

- Login mit lokaler Admin-Auth
- Liste pending Requests
- Detailansicht
- Approve
- Deny
- optional Kommentar beim Approve/Deny

### 3. Sessions

API:

- `GET /api/v1/sessions/{id}`
- `POST /api/v1/sessions/{id}/complete`
- `POST /api/v1/sessions/{id}/revoke`

Session Limits:

- expiresAt
- maxActions
- allowedCapabilities
- allowedTargets
- status: active/completed/revoked/expired

### 4. Actions

API:

- `POST /api/v1/sessions/{id}/actions`

MVP Action Types:

Dummy:

- `test.echo`
- `test.status.read`
- `test.logs.read`

SSH read-only:

- `ssh.command.read` nur für vorkonfigurierte, erlaubte Kommandos/Aktionsnamen.
- `system.status.read` als gemappte read-only Aktion.
- `disk.usage.read` als gemappte read-only Aktion.
- `service.status.read` als gemappte read-only Aktion mit erlaubten Service-Namen.

Dummy Actions werden vom Dummy Adapter beantwortet. SSH Actions laufen gegen konfigurierte SSH Targets, aber niemals als freie Shell.

### 5. Policy Engine minimal

MVP Policy Regeln:

- Capability muss im Request genehmigt worden sein
- Target muss im Request genehmigt worden sein
- Session muss aktiv sein
- Session darf nicht abgelaufen sein
- maxActions darf nicht überschritten sein
- write/high-risk Actions werden im MVP blockiert
- SSH Actions müssen gegen eine konfigurierte Target-Allowlist und Action-/Command-Allowlist laufen
- SSH Output wird begrenzt und auditiert; Secrets/Env-Dumps sind zu vermeiden

### 6. Audit Log

MVP Audit Events:

- AccessRequestCreated
- AccessRequestApproved
- AccessRequestDenied
- SessionCreated
- ActionExecuted
- ActionDenied
- SessionCompleted
- SessionRevoked
- SessionExpired
- AdminLoginSucceeded
- AdminLoginFailed

Speicherung zunächst in SQLite. Optional zusätzlich JSONL später.

## Nicht im MVP

- Home Assistant Adapter
- Docker Adapter
- Proxmox Adapter
- OIDC
- mTLS
- Passkeys
- Multi-Admin Approval
- Break-glass Shell
- Session Recording
- Policy UI
- Mobile Push Notifications

## Nach dem MVP

1. MVP-Hardening / Release Candidate nach dem SSH-read-only Connector
2. Generischer HTTP read-only Adapter
3. Docker read-only Adapter
4. Safe write Actions
5. Home Assistant Adapter
6. Proxmox Adapter
7. OIDC / Passkeys / mTLS
8. Hermes Toolset oder MCP Adapter
