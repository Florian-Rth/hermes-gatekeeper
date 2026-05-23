# Phase 3 — Session Actions und Dummy Adapter

> **For Hermes:** Use `subagent-driven-development`, `florian-backend-work`, and `florian-tdd`. Implement backend-first with integration tests as the primary proof.

## Ziel

Phase 3 schließt den ersten End-to-End-MVP-Kreis: Ein genehmigter Access Request erzeugt eine Session, und diese Session darf anschließend nur explizit erlaubte Dummy-Actions ausführen. Jede Action-Entscheidung und Ausführung wird auditiert.

Flow:

```text
Access Request -> Approve -> Session -> Execute allowed dummy action -> Audit
```

## Entscheidung

Wir bauen **Session Actions + Dummy Adapter** vor Web UI und Admin Login, weil damit der Gatekeeper-Kern beweisbar wird: kontrollierte, scoped, zeitbegrenzte Ausführung statt nur Approval-Verwaltung.

## In Scope

- Domain/Application:
  - Action-Ausführungsmodell für Session Actions.
  - Capability-Check gegen `Session.AllowedCapabilities`.
  - Active/Expired-Check gegen Session Status und `ExpiresAt`.
  - Dummy Adapter für sichere Test-Capabilities.
  - Audit Events für Action requested/allowed/denied/executed/failed.
- API:
  - `POST /api/v1/sessions/{sessionId}/actions`.
  - Body enthält `capability` und optionales `payload`.
- Persistenz:
  - Persistente Action-/Audit-Spur, mindestens über AuditEvents.
  - Falls für saubere API/Action-Historie nötig: eigene `SessionActions` Tabelle.
- Integrationstests:
  - kompletter Request -> Approve -> Execute allowed action Flow.
  - forbidden capability wird abgelehnt und auditiert.
  - unknown session wird abgelehnt.
  - expired session wird abgelehnt.
  - denied/non-active session falls Statusmodell es hergibt.
  - adapter failure wird sauber gemappt und auditiert.

## Nicht-Ziele

- Kein Frontend.
- Kein produktiver HomeLab/SSH/Home Assistant Adapter.
- Keine Raw-Shell-Ausführung.
- Keine Secrets oder externen Credentials.
- Keine vollständige Policy Engine.
- Kein Admin Login.

## API-Vertrag

### Execute Session Action

`POST /api/v1/sessions/{sessionId}/actions`

Request:

```json
{
  "capability": "test.echo",
  "payload": {
    "message": "hello"
  }
}
```

Success `200 OK`:

```json
{
  "sessionId": "...",
  "capability": "test.echo",
  "status": "succeeded",
  "result": {
    "message": "hello"
  }
}
```

Fehlersemantik:

- `400 Bad Request` bei invalidem Body oder unbekanntem Dummy-Payload-Format.
- `404 Not Found` bei unbekannter Session.
- `409 Conflict` bei expired/non-active Session oder Adapterfehler.
- `403 Forbidden` bei Capability außerhalb der Session-Allowlist.

## Dummy Capabilities

Initial erlaubte Dummy-Capabilities:

- `test.echo`
  - Payload: `{ "message": "..." }`
  - Result: `{ "message": "..." }`
- `test.status.read`
  - Payload optional/leer.
  - Result: `{ "status": "ok" }`
- `test.fail`
  - Bewusster Fehlerpfad für Tests.
  - Nur ausführen, wenn capability explizit erlaubt ist.
  - Resultiert in sauberem Adapterfehler und Audit.

## Integrationstest-Flows

1. `Should_ExecuteAllowedDummyAction_When_RequestApprovedSessionAllowsCapability`
   - Create AccessRequest mit `requestedCapabilities = ["test.echo"]`.
   - Approve mit Admin Token.
   - POST Action `test.echo`.
   - Assert `200 OK`, Result enthält Echo Message.
   - Assert Audit enthält Request/Approval/SessionCreated/Action Events.

2. `Should_ReturnForbidden_When_ActionCapabilityIsNotAllowed`
   - Create AccessRequest mit `requestedCapabilities = ["test.status.read"]`.
   - Approve.
   - POST Action `test.echo`.
   - Assert `403 Forbidden`.
   - Assert denied Audit Event wurde geschrieben.

3. `Should_ReturnNotFound_When_SessionDoesNotExist`
   - POST Action mit random SessionId.
   - Assert `404 Not Found`.

4. `Should_ReturnConflict_When_SessionIsExpired`
   - Create AccessRequest mit sehr kurzer Session oder seed expired Session direkt über DbContext.
   - POST Action.
   - Assert `409 Conflict`.
   - Assert denied/failed Audit Event.

5. `Should_ReturnConflict_When_DummyAdapterFails`
   - Session erlaubt `test.fail`.
   - POST Action `test.fail`.
   - Assert `409 Conflict`.
   - Assert failed Audit Event.

## Implementierungsslices

### Slice 3.1 — Application Action Service

- `ExecuteSessionActionCommand`.
- `SessionActionResult` / Details-Record.
- `ISessionActionService` + `SessionActionService`.
- `ISessionActionAdapter` Port.
- Ergebnisfälle: success, not found, forbidden, conflict, validation.

### Slice 3.2 — Dummy Adapter

- `DummySessionActionAdapter` in Infrastructure oder Application-naher Infrastructure.
- Unterstützte Capabilities: `test.echo`, `test.status.read`, `test.fail`.
- Keine externen IOs.

### Slice 3.3 — API Endpoint

- `ExecuteSessionActionEndpoint` in `Gatekeeper.Api/Endpoints/Sessions`.
- Validator für route id, capability und payload.
- Mapping Request -> Command -> Response.

### Slice 3.4 — Integration Tests und Audit

- API Integrationstests für alle oben genannten Flows.
- Audit Event Factories ergänzen.
- Keine EF Entities über API/Application leaken.

## Validation Gate

```bash
dotnet restore backend/Gatekeeper.sln
dotnet test backend/Gatekeeper.sln
cd backend && dotnet csharpier format . && dotnet csharpier check .
dotnet build backend/Gatekeeper.sln --no-restore
docker compose config
docker compose build backend
```

## Commit Boundary

Phase 3 endet mit Commit und Push, wenn:

- der End-to-End-Flow Request -> Approve -> Session -> Action über HTTP grün getestet ist.
- Forbidden, NotFound, Expired und Adapter-Failure Integrationstests grün sind.
- Audit Events für Action-Entscheidungen vorhanden sind.
- keine produktiven Adapter, Raw-Shell oder Secrets eingeführt wurden.

Commit-Vorschlag:

```text
feat: add session actions with dummy adapter
```

## Agenten-Fallen

- Keine produktive Infrastruktur anbinden.
- Keine Raw Shell bauen.
- Keine UI in Phase 3 ziehen.
- Nicht nur Unit Tests: die relevanten Flows müssen als Integrationstests über HTTP laufen.
- Capability-Check muss vor Adapterausführung passieren.
- Adapterfehler dürfen nicht als erfolgreiche Actions erscheinen.
- Payload/Result nicht unbegrenzt oder geheimnisträchtig in Audit schreiben.
