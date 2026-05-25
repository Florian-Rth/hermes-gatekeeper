# Hermes Gatekeeper — Current Project Status

Last updated: 2026-05-25
Current branch: `main`
Latest product/deploy commit: `2cfe218 feat: replace admin token UI with login session`

## Executive Summary

Hermes Gatekeeper has a working backend MVP core through Phase 5, a session lifecycle/audit web UI through Phase 6, and local admin cookie authentication through Phase 7:

```text
Access Request -> Admin Login -> Approve/Deny -> Session -> Execute typed dummy action -> Lifecycle controls in UI -> Audit browsing UI
```

The backend is implemented with .NET 10, ASP.NET Core/FastEndpoints, EF Core, SQLite, migrations, and integration tests. The frontend now has a browser dashboard for listing requests, reviewing details, logging in as local admin, approving/denying, viewing session lifecycle state, running allowed dummy actions, and browsing audit events.

Phase 5 added backend session lifecycle controls, action-budget enforcement, lazy expiry, and an admin audit listing API. Phase 6 exposed those capabilities in the web UI with compound SessionLifecycleCard and AuditFeed components. Phase 7 replaced the manually entered visible admin token with local admin login and HttpOnly cookie-backed browser session auth for admin operations. Future agents should treat the backend action loop, lifecycle controls, audit API, lifecycle/audit UI, and local admin login/session flow as implemented and validated. Do not re-plan or rebuild Phases 0-7 unless the repository state contradicts this document.

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

### Phase 5 — Session Lifecycle and Audit Visibility Backend

Commit: `47502c8 feat: add session lifecycle and audit controls`

Implemented:

- Session lifecycle states beyond `Active`:
  - `Completed`
  - `Revoked`
  - `Expired`
- Session lifecycle endpoints:
  - `POST /api/v1/sessions/{id}/complete`
  - `POST /api/v1/sessions/{id}/revoke`
- Lazy expiry materialization on relevant session access.
- Action budget tracking:
  - `ActionCount`
  - `MaxActionCount`
  - `GATEKEEPER_SESSION_MAX_ACTION_COUNT` with fallback semantics.
- Atomic action budget reservation before adapter dispatch.
- Parallel action requests cannot exceed the action budget.
- Terminal sessions block action execution without consuming budget.
- Admin audit listing API:
  - `GET /api/v1/audit-events`
  - filters: `aggregateId`, `eventType`, `from`, `to`
  - cursor/limit pagination
  - bounded details exposure.
- Audit events:
  - `SessionCompleted`
  - `SessionRevoked`
  - `SessionExpired`
  - `ActionCountExceeded`

Validation for Phase 5:

- `dotnet csharpier check .`: passed.
- `dotnet build Gatekeeper.sln --no-restore`: passed, 0 warnings.
- `dotnet test Gatekeeper.sln --no-restore`: passed, 120/120.
- Final integration review: APPROVED.

Important current limitation:

- Phase 5 is backend-only. The web UI does not yet expose revoke/complete, action budget, terminal status details, or audit browsing.

### Phase 6 — Session Lifecycle and Audit Visibility UI

Commit: `8a3e345 feat: add session lifecycle audit ui`

Implemented:

- Phase plan: `docs/phase-6-session-lifecycle-audit-ui.md`.
- Shared in-memory admin token context:
  - token stays in React state only.
  - no localStorage/sessionStorage/cookie persistence.
  - non-sensitive token version is used for audit query cache isolation.
- Extended frontend API boundary and TanStack Query hooks for:
  - Phase 5 session lifecycle fields.
  - complete/revoke session mutations.
  - audit event listing with filters and cursor pagination.
- Compound `SessionLifecycleCard` with context-backed slots:
  - status, budget, capabilities, timestamps, and lifecycle actions.
  - complete/revoke controls for active sessions.
  - revoke confirmation and admin-token requirement.
  - terminal sessions render read-only state.
- Compound `AuditFeed` with context-backed slots:
  - filters for aggregateId, eventType, from, and to.
  - cursor pagination.
  - loading, empty, error, and list states.
  - bounded key/value details rendering.
- Dashboard integration:
  - audit section in the existing request review flow.
  - default audit filter by selected request id.
  - no global session list assumption.

Validation for Phase 6:

- `cd frontend && pnpm check`: passed.
- `cd frontend && pnpm test -- --run`: passed, 24/24 tests.
- `cd frontend && pnpm build`: passed.
- `docker compose config`: passed.
- `docker compose build frontend`: passed.
- Slice spec reviews: PASS.
- Slice code quality/security reviews: APPROVED.

Important current limitation:

- There is still no full admin login/cookie auth; the UI uses the existing manually entered static admin token.
- There is no global session list endpoint/UI; the UI works with known sessions from the request/approval flow.

### Phase 7 — Admin Authentication Hardening

Backend commit: `a4a4ced feat: add local admin cookie authentication`
Frontend commit: `2cfe218 feat: replace admin token UI with login session`

Implemented:

- Phase plan: `docs/phase-7-admin-authentication-hardening.md`.
- Local single-admin authentication using environment-seeded credentials:
  - `GATEKEEPER_ADMIN_USERNAME`
  - `GATEKEEPER_ADMIN_PASSWORD`
  - `GATEKEEPER_ADMIN_COOKIE_SECURE`
  - `GATEKEEPER_ADMIN_SESSION_IDLE_MINUTES`
- Admin session endpoints:
  - `POST /api/v1/admin/login`
  - `POST /api/v1/admin/logout`
  - `GET /api/v1/admin/me`
- HttpOnly cookie-backed browser admin session with SameSite/Secure settings and local-development Secure override.
- Minimal same-origin/CSRF protection for unsafe cookie-authenticated admin requests.
- In-memory login rate limiting.
- Admin audit events:
  - `AdminLoginSucceeded`
  - `AdminLoginFailed`
  - `AdminLogout`
- Admin-protected operations now use the admin cookie session instead of `X-Gatekeeper-Admin-Token`:
  - approve
  - deny
  - revoke
  - audit event listing
- `POST /api/v1/sessions/{id}/complete` remains unchanged and is not silently moved behind admin auth.
- Frontend admin-auth feature module under `frontend/src/features/admin-auth`.
- `AdminAuthProvider`, `AdminAuthGate`, and local login panel.
- App header shows authenticated admin username and logout action.
- API client sends same-origin credentials so HttpOnly cookies can be used without exposing session secrets to JavaScript.
- Frontend tests cover login, failed login, logout, explicit session expiry handling, protected admin operations without token headers, and audit filtering.

Validation for Phase 7:

- Backend `dotnet restore/build/test`: passed, 128/128 tests.
- Backend CSharpier check: passed.
- `docker compose config`: passed.
- `docker compose build backend`: passed.
- `cd frontend && pnpm check`: passed.
- `cd frontend && pnpm test -- --run`: passed, 28/28 tests.
- `cd frontend && pnpm build`: passed.
- `docker compose build frontend`: passed.
- Spec review: PASS.
- Frontend quality/security review: APPROVED.

Important current limitation:

- Local single-admin auth only; no OIDC, TOTP, Passkeys/WebAuthn, mTLS, or multi-admin approval.
- Login rate limiting is in-memory only.
- No global session list endpoint/UI; the UI works with known sessions from the request/approval flow.

## Current API Surface

### Health

- `GET /health`

### Access Requests

- `POST /api/v1/access-requests`
- `GET /api/v1/access-requests/{id}`
- `GET /api/v1/access-requests`
- `POST /api/v1/access-requests/{id}/approve`
- `POST /api/v1/access-requests/{id}/deny`

Approval/deny requires an authenticated admin browser session. The UI obtains it via `POST /api/v1/admin/login` and the browser sends the HttpOnly admin cookie on same-origin requests.

### Admin Auth

- `POST /api/v1/admin/login`
- `POST /api/v1/admin/logout`
- `GET /api/v1/admin/me`

Configured by:

```text
GATEKEEPER_ADMIN_USERNAME
GATEKEEPER_ADMIN_PASSWORD
GATEKEEPER_ADMIN_COOKIE_SECURE
GATEKEEPER_ADMIN_SESSION_IDLE_MINUTES
```

### Sessions

- `GET /api/v1/sessions/{id}`
- `POST /api/v1/sessions/{sessionId}/actions`
- `POST /api/v1/sessions/{id}/complete`
- `POST /api/v1/sessions/{id}/revoke`

Session action request shape:

```json
{
  "capability": "test.echo",
  "payload": {
    "message": "hello"
  }
}
```


### Audit Events

- `GET /api/v1/audit-events`

Audit listing requires an authenticated admin browser session.

Supported filters:

- `aggregateId`
- `eventType`
- `from`
- `to`
- `cursor`
- `limit`

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

- No global session list endpoint/UI yet.
- No action history UI beyond the audit event API.
- No agent authentication for request creation/action execution beyond current MVP boundaries.
- No real target adapters.
- No production/HomeLab integration.
- No raw shell and no break-glass flow.
- No OIDC, mTLS, TOTP, Passkeys, or multi-admin approval.

## Recommended Next Phase

Recommended next phase: MVP hardening / release-candidate preparation before real target adapters.

Reason:

- The full dummy MVP flow now works behind local admin login, session lifecycle controls, action budget visibility, and audit browsing.
- Before adding productive adapters, the project should tighten documentation, error responses, demo flow, security headers/CORS posture, and release-readiness checks.

Suggested scope:

- Keep productive adapters, raw shell, OIDC, mTLS, TOTP, Passkeys, and multi-admin approval out of the immediate next phase unless explicitly chosen.
- Polish README demo flow and operational guidance.
- Add final MVP integration/security checks and known-limitations documentation.
- Prepare a release-candidate boundary for the dummy adapter MVP.

## Important Agent Instructions

Future agents should:

1. Read this file first when asked “where are we?” on Gatekeeper.
2. Cross-check with `git status --short --branch` and `git log --oneline -5` before reporting status.
3. Do not assume the older phase numbering in `docs/implementation-plan.md` is exact; implementation was intentionally re-scoped:
   - Approval + sessions were completed before full admin login.
   - Session actions + dummy adapter were completed before frontend UI.
   - Minimal approval web UI is implemented.
   - Backend lifecycle/audit controls and their frontend UI are implemented.
   - Local admin login/cookie auth is implemented in Phase 7.
4. Keep all productive-system access behind typed adapters and explicit approvals.
5. Do not add raw shell or real HomeLab adapters until Florian explicitly chooses that phase.
6. Keep using integration tests for full HTTP flows, especially security and audit behavior.
