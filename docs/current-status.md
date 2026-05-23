# Hermes Gatekeeper — Current Project Status

Last updated: 2026-05-23
Current branch: `main`
Latest completed commit: `e06e294 docs: update gatekeeper implementation plan governance`

## Executive Summary

Hermes Gatekeeper has a working backend MVP core through Phase 3 and a minimal approval web UI through Phase 4:

```text
Access Request -> Approve/Deny -> Session -> Execute typed dummy action -> Audit
```

The backend is implemented with .NET 10, ASP.NET Core/FastEndpoints, EF Core, SQLite, migrations, and integration tests. The frontend now has a minimal browser dashboard for listing requests, reviewing details, entering the static admin token, approving/denying, viewing the created session summary, and optionally running a dummy session action.

Future agents should treat the backend action loop and minimal approval UI as implemented and validated. Do not re-plan or rebuild Phases 0-4 unless the repository state contradicts this document.

## Implemented and Committed

### Phase 0 — Project Foundation

Commit: `a59bb3e feat: scaffold gatekeeper full-stack foundation`

Implemented:

- Backend solution under `backend/`:
  - `Gatekeeper.Api`
  - `Gatekeeper.Application`
  - `Gatekeeper.Core`
  - `Gatekeeper.Infrastructure`
  - `Gatekeeper.Tests`
- ASP.NET Core/FastEndpoints API.
- Health endpoint.
- React/Vite frontend scaffold.
- Docker Compose baseline.
- Backend/frontend validation baseline.

### Phase 1 — Access Request Domain, Persistence, and API

Commit: `b041130 feat: add access request persistence and api`

Implemented:

- `AccessRequest` domain model.
- `AccessRequestStatus` and `RiskLevel`.
- `AuditEvent` domain model.
- EF Core SQLite persistence and initial migration.
- API endpoints:
  - `POST /api/v1/access-requests`
  - `GET /api/v1/access-requests/{id}`
  - `GET /api/v1/access-requests`
- Access request validation.
- Audit event `AccessRequestCreated`.
- API integration tests for create/get/list and validation failures.

### Phase 2 — Admin Approval Flow and Sessions

Commit: `5fe72cf feat: add approval flow and sessions`

Implemented:

- Access request status transitions:
  - `Pending -> Approved`
  - `Pending -> Denied`
- Session domain model with:
  - `Id`
  - `AccessRequestId`
  - `Status`
  - `AllowedTargets`
  - `AllowedCapabilities`
  - `CreatedAt`
  - `ExpiresAt`
- EF Core session persistence and migration.
- Unique index on `Sessions.AccessRequestId`.
- Approval/denial application services.
- Admin token guard for approval endpoints via `X-Gatekeeper-Admin-Token` and `GATEKEEPER_ADMIN_TOKEN`.
- Fixed-time token comparison.
- Concurrency protection for stale pending approval/denial races.
- API endpoints:
  - `POST /api/v1/access-requests/{id}/approve`
  - `POST /api/v1/access-requests/{id}/deny`
  - `GET /api/v1/sessions/{id}`
- Audit events:
  - `AccessRequestApproved`
  - `AccessRequestDenied`
  - `SessionCreated`
- Integration tests for auth failures, not found, conflict, session retrieval, and concurrency behavior.

Important current limitation:

- Approval uses a static admin token, not full admin login/cookie auth.

### Phase 3 — Session Actions and Dummy Adapter

Commit: `7625807 feat: add session actions with dummy adapter`

Implemented:

- Session action endpoint:
  - `POST /api/v1/sessions/{sessionId}/actions`
- Application service:
  - `ISessionActionService`
  - `SessionActionService`
  - `ExecuteSessionActionCommand`
  - session action result/outcome models
- Adapter port:
  - `ISessionActionAdapter`
- Dummy infrastructure adapter:
  - `DummySessionActionAdapter`
- Dummy capabilities:
  - `test.echo`
  - `test.status.read`
  - `test.fail`
- Capability allowlist enforcement before adapter execution.
- Active and expiry checks before adapter execution.
- Controlled result mapping:
  - `200 OK` for success
  - `400 Bad Request` for invalid action payload
  - `403 Forbidden` for forbidden capability
  - `404 Not Found` for unknown session
  - `409 Conflict` for expired/inactive sessions or adapter failure
- Audit events:
  - `SessionActionRequested`
  - `SessionActionAllowed`
  - `SessionActionDenied`
  - `SessionActionExecuted`
  - `SessionActionFailed`

Security-relevant behavior:

- No raw shell execution.
- No HomeLab, SSH, Docker, Proxmox, Home Assistant, or other productive adapter.
- No secrets or external credentials introduced.
- Audit payloads intentionally do not store arbitrary raw action payloads or full action outputs. They store constrained metadata: session id, access request id, capability, and reason.

Integration tests cover:

- Full HTTP flow: request -> approve -> session -> `test.echo` action.
- Happy-path audit chain:
  - `AccessRequestCreated`
  - `AccessRequestApproved`
  - `SessionCreated`
  - `SessionActionRequested`
  - `SessionActionAllowed`
  - `SessionActionExecuted`
- `test.status.read` success path.
- invalid dummy payload -> `400 Bad Request` + failed audit.
- forbidden capability -> `403 Forbidden` + denied audit.
- unknown session -> `404 Not Found`.
- expired session -> `409 Conflict` + denied audit.
- dummy adapter failure -> `409 Conflict` + failed audit.

Final validation for Phase 3:

- `dotnet test Gatekeeper.sln`: 62/62 tests passed.
- CSharpier check: passed.
- `dotnet build Gatekeeper.sln --no-restore`: 0 warnings, 0 errors.
- `docker compose config`: passed.
- `docker compose build backend`: passed.

Note: The host VM did not have `dotnet` installed during Phase 3. Validation was run through the official `mcr.microsoft.com/dotnet/sdk:10.0` Docker image.

### Phase 4 — Minimal Approval Web UI

Commit: `35f2eec feat: add minimal approval web ui`

Implemented:

- Phase plan: `docs/phase-4-minimal-approval-web-ui.md`.
- Frontend feature module under `frontend/src/features/access-requests`.
- Zod-validated API boundary and TanStack Query hooks for:
  - listing access requests.
  - loading request details.
  - approving requests with `X-Gatekeeper-Admin-Token`.
  - denying requests with `X-Gatekeeper-Admin-Token`.
  - loading session details after approval.
  - executing an optional dummy action.
- Admin token input held only in React state. It is not stored in localStorage/sessionStorage.
- Minimal dashboard UI:
  - request list with pending focus.
  - human-readable request details.
  - approve/deny decision panel with optional comment.
  - session summary after approval.
  - optional `test.echo` / `test.status.read` demo action.
  - loading, empty, warning and error states.
- Dev/runtime routing:
  - Vite proxies `/api` and `/health` to `http://localhost:5209`.
  - Nginx proxies `/api` and `/health` to Compose service `backend:8080`.
- Tests:
  - app renders approval dashboard.
  - dashboard loads requests and details.
  - approve sends admin token header and can run dummy action.

Validation for Phase 4:

- `pnpm check`: passed.
- `pnpm test -- --run`: 2 files, 3 tests passed.
- `pnpm build`: passed.
- `docker compose config`: passed.
- `docker compose build frontend`: passed.
- Spec/UX review: PASS.
- Frontend quality/security review: APPROVED.

Important current limitation:

- This is not full admin authentication. The UI uses the existing static admin token manually entered by the user.

## Current API Surface

### Health

- `GET /health`

### Access Requests

- `POST /api/v1/access-requests`
- `GET /api/v1/access-requests/{id}`
- `GET /api/v1/access-requests`
- `POST /api/v1/access-requests/{id}/approve`
- `POST /api/v1/access-requests/{id}/deny`

Approval/deny requires:

```http
X-Gatekeeper-Admin-Token: <token>
```

Server-side token source:

```text
GATEKEEPER_ADMIN_TOKEN
```

### Sessions

- `GET /api/v1/sessions/{id}`
- `POST /api/v1/sessions/{sessionId}/actions`

Session action request shape:

```json
{
  "capability": "test.echo",
  "payload": {
    "message": "hello"
  }
}
```

Session action success response shape:

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

## Current Known Limitations

These are not bugs in the current phase; they are intentionally deferred scope:

- No full admin login/cookie auth yet.
- No session revoke endpoint yet.
- No session complete endpoint yet.
- No max action count tracking yet.
- `SessionStatus` currently only has `Active`.
- No action history endpoint beyond audit events.
- No audit browsing API/UI yet.
- No agent authentication for request creation/action execution beyond current MVP boundaries.
- No real target adapters.
- No production/HomeLab integration.
- No raw shell and no break-glass flow.
- No OIDC, mTLS, TOTP, Passkeys, or multi-admin approval.

## Recommended Next Phase

Recommended next phase: Backend hardening for session lifecycle and audit browsing.

Reason:

- The minimal UI can approve/deny and show created sessions.
- The next major product gap is lifecycle control after approval.
- Revocation, completion, max action count, and audit browsing make sessions safer and more operable.

Suggested scope:

- `POST /api/v1/sessions/{id}/revoke`.
- `POST /api/v1/sessions/{id}/complete`.
- max action count tracking.
- action history or audit listing/filter API.
- UI wiring for revoke/complete and audit visibility.

Alternative if backend lifecycle is postponed:

- Full local admin login/cookie auth to replace manual static token entry.

## Important Agent Instructions

Future agents should:

1. Read this file first when asked “where are we?” on Gatekeeper.
2. Cross-check with `git status --short --branch` and `git log --oneline -5` before reporting status.
3. Do not assume the older phase numbering in `docs/implementation-plan.md` is exact; implementation was intentionally re-scoped:
   - Approval + sessions were completed before full admin login.
   - Session actions + dummy adapter were completed before frontend UI.
   - Minimal approval web UI is now implemented, but full admin login is still deferred.
4. Keep all productive-system access behind typed adapters and explicit approvals.
5. Do not add raw shell or real HomeLab adapters until Florian explicitly chooses that phase.
6. Keep using integration tests for full HTTP flows, especially security and audit behavior.
