# Hermes Gatekeeper — MVP Scope Draft

## Ziel des MVP

Der MVP soll den generischen Kern von Hermes Gatekeeper beweisen:

1. Ein Agent kann eine strukturierte Access Request per HTTP API erstellen.
2. Ein Admin kann die Anfrage über lokale Web-UI/Admin-Auth genehmigen oder ablehnen.
3. Bei Genehmigung entsteht eine begrenzte Session.
4. Der Agent kann innerhalb der Session nur erlaubte Actions ausführen.
5. Alle Schritte werden auditiert.
6. Das System ist per Docker Compose selbsthostbar.

Nicht Ziel des MVP: direkte produktive HomeLab-Integration.

## Tech Stack

- .NET / ASP.NET Core, neueste stabile Version
- FastEndpoints
- React Frontend
- SQLite
- Docker Compose
- lokale Admin-Auth
- OpenAPI/Swagger

## MVP-Leitentscheidung

Der MVP startet mit einem generischen Dummy/Test Adapter, nicht mit Home Assistant.

Begründung:

- beweist das Produktmodell ohne produktive Risiken
- hält den Kern generisch
- vermeidet frühe HA-spezifische Modellverzerrung
- ist für Open-Source-Nutzer verständlicher
- erlaubt End-to-End Tests ohne echte Zielsysteme

## MVP Features

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

- `test.echo`
- `test.status.read`
- `test.logs.read`

Diese Actions werden vom Dummy Adapter beantwortet, ohne echte Zielsysteme zu berühren.

### 5. Policy Engine minimal

MVP Policy Regeln:

- Capability muss im Request genehmigt worden sein
- Target muss im Request genehmigt worden sein
- Session muss aktiv sein
- Session darf nicht abgelaufen sein
- maxActions darf nicht überschritten sein
- write/high-risk Actions werden im Dummy MVP blockiert, außer explizit erlaubt

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
- SSH Adapter
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

1. Generischer HTTP read-only Adapter
2. SSH read-only Adapter für Test-VM
3. Docker read-only Adapter
4. Safe write Actions
5. Home Assistant Adapter
6. Proxmox Adapter
7. OIDC / Passkeys / mTLS
8. Hermes Toolset oder MCP Adapter
