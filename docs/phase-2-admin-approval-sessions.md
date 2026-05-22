# Phase 2 — Admin Approval Flow und Sessions

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

## Ziel

Phase 2 baut den backendseitigen Approval-Flow: Pending Access Requests können genehmigt oder abgelehnt werden. Bei Approval entsteht eine begrenzte Session. Alle Statuswechsel werden auditiert.

## Grill-Me Ergebnis

Eskalationsbedarf: nein.

Die Phase wird bewusst backend-first geschnitten. Kein Frontend, keine Actions, kein Dummy Adapter. Approval bekommt eine minimale, klar getrennte Admin-Token-Grenze, damit keine anonymen Approve/Deny-Endpunkte entstehen.

## Selbst entschiedene Optionen

| Thema | Verworfene Optionen | Entscheidung | Grund |
| --- | --- | --- | --- |
| Phase-2-Schnitt | komplette Admin UI; kompletter Action Broker | Backend-only Approval + Session-Erzeugung | Liefert den nächsten fachlichen Kern ohne UI-/Adapter-Scope-Creep. |
| Auth | anonym; vollständige Login/Cookie-Session; Fake-Auth | statischer Admin Token per `GATEKEEPER_ADMIN_TOKEN` für Approval-Endpunkte | Approval darf nicht anonym sein; vollständige lokale Admin-Auth bleibt spätere UI/Auth-Phase. |
| Agent Auth | sofort nachziehen | nicht in dieser Phase | Bestehende AccessRequest-API bleibt unverändert; separate Auth-Härtung später. |
| Session-Modell | nur SessionId als String; kompletter Action-Zähler/Policy | echte Session-Domain + EF-Persistenz mit Status/ExpiresAt/Allowlists | Approval braucht persistente Session; Actions/MaxActions-Verbrauch kommen später. |
| API-Pfade | unversioniert; `/admin/*` | `/api/v1/access-requests/{id}/approve`, `/deny`, `GET /api/v1/sessions/{id}` | Passt zum bestehenden versionierten API-Vertrag. |
| Audit | nur Status ändern | `AccessRequestApproved`, `AccessRequestDenied`, `SessionCreated` | Audit ist MVP-Kern. |
| Re-Approval/Re-Deny | idempotent erlauben | nur `Pending` darf approved/denied werden; sonst `409 Conflict` | Statusübergänge müssen explizit geschützt sein. |
| Session Dauer | fix 15 Minuten; Admin setzt beliebig | initial aus `AccessRequest.DurationMinutes` | Keine neue Policy-Komplexität. |

## In Scope

- Domain:
  - `AccessRequest.Approve(now)` und `AccessRequest.Deny(now)` mit Statusübergangsprüfung.
  - `Session` und `SessionStatus`.
- Application:
  - Approve/Deny Commands.
  - Approval Service Methoden.
  - Session Details und Repository-Port.
  - Audit Events für Approval/Denial/SessionCreated.
- Infrastructure:
  - EF Entity/DbSet/Migration für Sessions.
  - Repository für Sessions.
  - AccessRequest Repository muss Statusupdates speichern können.
- API:
  - `POST /api/v1/access-requests/{id}/approve`
  - `POST /api/v1/access-requests/{id}/deny`
  - `GET /api/v1/sessions/{id}`
  - Admin-Token-Prüfung für approve/deny per Header `X-Gatekeeper-Admin-Token`.
- Tests nach TDD:
  - Domain status transitions.
  - Application approval/denial behavior.
  - EF roundtrip for updated request + session.
  - API integration tests including unauthorized/forbidden/conflict/not-found paths.

## Nicht-Ziele

- Kein Frontend.
- Kein Login/Cookie/Auth-UI.
- Keine Agent-Token-Härtung der bestehenden Request-Endpunkte.
- Keine Actions.
- Kein Dummy Adapter.
- Keine Session complete/revoke Endpunkte.
- Keine Policy Engine über einfache Status-/Allowlist-Daten hinaus.
- Keine Produktivsystem-Integration.

## API-Vertrag

### Approve

`POST /api/v1/access-requests/{id}/approve`

Header:

`X-Gatekeeper-Admin-Token: <token>`

Body optional:

```json
{
  "comment": "Looks safe for read-only diagnostics."
}
```

Success `200 OK`:

```json
{
  "accessRequestId": "...",
  "status": "approved",
  "sessionId": "...",
  "expiresAt": "..."
}
```

Statuscodes:

- `200 OK` bei erfolgreichem Approval.
- `401 Unauthorized` wenn Header fehlt.
- `403 Forbidden` wenn Token falsch ist oder kein Admin Token konfiguriert ist.
- `404 Not Found` wenn Request nicht existiert.
- `409 Conflict` wenn Request nicht mehr pending ist.

### Deny

`POST /api/v1/access-requests/{id}/deny`

Header:

`X-Gatekeeper-Admin-Token: <token>`

Body optional:

```json
{
  "comment": "Scope is too broad."
}
```

Success `200 OK`:

```json
{
  "accessRequestId": "...",
  "status": "denied"
}
```

Statuscodes analog zu Approve, außer keine Session.

### Get Session

`GET /api/v1/sessions/{id}`

Success `200 OK`:

```json
{
  "id": "...",
  "accessRequestId": "...",
  "status": "active",
  "allowedTargets": ["prod-api"],
  "allowedCapabilities": ["logs:read"],
  "createdAt": "...",
  "expiresAt": "..."
}
```

## Implementierungsslices

### Slice 2.1 — Domain Status Transitions und Session

Ziel: Domain kann pending Requests genehmigen/ablehnen und Sessions modellieren.

Aktionen:

- Tests in `backend/tests/Gatekeeper.Tests/AccessRequestDomainTests.cs` ergänzen:
  - pending request can be approved and updates timestamp.
  - pending request can be denied and updates timestamp.
  - approved/denied request cannot transition again.
  - session created from approved request carries allowlists and expiry.
- `AccessRequest.Approve(DateTimeOffset now)` und `.Deny(DateTimeOffset now)` implementieren.
- `Session` und `SessionStatus` unter `Gatekeeper.Core/Sessions` anlegen.
- `AuditEvent` Factory-Methoden ergänzen.

### Slice 2.2 — Application Approval Service

Ziel: Approve/Deny Use Cases schreiben AccessRequest, Session und AuditEvents atomar.

Aktionen:

- `ApproveAccessRequestCommand`, `DenyAccessRequestCommand`.
- `ApprovalResult`, `DenialResult`, `SessionDetails`.
- `ISessionRepository`.
- `IAccessRequestRepository.UpdateAsync(...)` ergänzen.
- `IAccessRequestService` um `ApproveAsync`, `DenyAsync` erweitern.
- Tests in `AccessRequestServiceTests.cs`:
  - approve creates active session and audit chain.
  - deny writes audit and creates no session.
  - missing request returns not found result.
  - non-pending returns conflict result.

### Slice 2.3 — EF Session Persistenz und Migration

Ziel: Sessions und Statusupdates werden in SQLite persistiert.

Aktionen:

- `SessionEntity`, `DbSet<Sessions>`, Mapping und Repository ergänzen.
- `EfAccessRequestRepository.UpdateAsync` implementieren.
- Migration für Sessions hinzufügen.
- Tests in `EfSqlitePersistenceTests.cs`:
  - status update roundtrip.
  - approved request + session + audit events persist atomically.
  - session load by id.

### Slice 2.4 — API Endpoints und Admin Token Guard

Ziel: HTTP Approval Flow funktioniert mit klarer Admin-Token-Grenze.

Aktionen:

- `IAdminTokenValidator`/`AdminTokenValidator` in Application oder Api-nahem Infrastructure-Code.
- Header `X-Gatekeeper-Admin-Token` prüfen.
- Endpunkte:
  - `ApproveAccessRequestEndpoint`
  - `DenyAccessRequestEndpoint`
  - `GetSessionEndpoint`
- API Tests:
  - missing token -> 401.
  - wrong token -> 403.
  - correct token approves and response contains session id.
  - deny sets denied.
  - second approve/deny returns 409.
  - get session returns session details.

### Slice 2.5 — Validation Gate und Cleanup

Validierung:

```bash
dotnet restore backend/Gatekeeper.sln
dotnet test backend/Gatekeeper.sln
cd backend && dotnet csharpier format . && dotnet csharpier check .
dotnet build backend/Gatekeeper.sln --no-restore
docker compose config
docker compose build backend
```

## Commit Boundary

Phase 2 endet mit einem Commit und Push, wenn:

- alle Backend-Tests grün sind.
- Solution ohne Warnungen baut.
- CSharpier angewendet/geprüft wurde.
- EF Migration für Sessions enthalten ist.
- approve/deny nicht anonym nutzbar sind.
- keine UI, Actions oder Adapter enthalten sind.

Commit-Vorschlag:

```text
feat: add approval flow and sessions
```

## Agenten-Fallen

- Keine anonymen Approval-Endpunkte.
- Keine vollständige Admin-Login-UI bauen.
- Keine Actions oder Dummy Adapter in diese Phase ziehen.
- Keine Raw-Shell/produktiven Adapter.
- Keine EF Entities über Application/API Boundaries leaken.
- Keine FluentAssertions.
- Keine Primary Constructors.
- Keine XML-Kommentare.
