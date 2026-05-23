# Phase 5 — Session Lifecycle and Audit Visibility Backend

## Goal

Harden the backend session lifecycle so approved sessions are revocable, completable, bounded by an action limit, expiry-aware, and auditable through a minimal admin API.

This is a backend-only phase. Frontend wiring for lifecycle controls and audit visibility is intentionally deferred to a separate follow-up phase.

## Grill-Me Decisions

- Backend and frontend are handled in separate phases by default.
- This phase is backend-first only.
- Session lifecycle uses a simple terminal state model:
  - `Active -> Completed`
  - `Active -> Revoked`
  - `Active -> Expired`
- `Completed`, `Revoked`, and `Expired` are terminal. No reopen or extend behavior in this phase.
- `POST /api/v1/sessions/{id}/revoke` requires the existing admin token header: `X-Gatekeeper-Admin-Token`.
- `GET /api/v1/audit-events` requires the existing admin token header: `X-Gatekeeper-Admin-Token`.
- `POST /api/v1/sessions/{id}/complete` does not introduce new client authentication in this phase. Future Hermes client token, mTLS, or request-signing hardening remains a separate backend-security phase.
- `MaxActionCount` is copied into the session at creation time from `GATEKEEPER_SESSION_MAX_ACTION_COUNT` with fallback `10`.
- Action budget is consumed only by authorized adapter dispatches after lifecycle and capability checks.
- Authorized adapter dispatches consume budget even when the adapter returns a controlled failure.
- Invalid requests, forbidden capabilities, unknown sessions, and completed/revoked/expired sessions do not consume action budget.
- Audit listing is a minimal generic admin feed: `GET /api/v1/audit-events` with cursor/limit pagination and filters for `aggregateId`, `eventType`, `from`, and `to`.
- Audit listing response exposes bounded summary/details data, not uncontrolled raw payload/output.
- Expiry is lazy and server-side materialized on relevant session access.
- `GET /api/v1/sessions/{id}` materializes expiry.
- `complete` and `revoke` on an already expired active session first materialize `Expired` and then return `409 Conflict`.
- `SessionExpired` is audited only once, on the first successful `Active -> Expired` transition.
- No background expiry worker in this phase.
- Action execution uses atomic budget reservation before adapter dispatch.
- No long database transaction is held over adapter I/O.

## Non-goals

- No frontend implementation.
- No productive target adapters.
- No raw shell.
- No OIDC, Passkeys, TOTP, mTLS, or Hermes client-token implementation.
- No complex policy DSL.
- No tamper-proof audit hash chain.
- No HomeLab, SSH, Docker, or Home Assistant integration.

## Implementation Slices

### Slice 1 — Domain, configuration, persistence, and migration

- Extend `SessionStatus` with `Completed`, `Revoked`, and `Expired`.
- Add persisted session fields:
  - `ActionCount`
  - `MaxActionCount`
  - lifecycle timestamp fields where useful, e.g. `CompletedAt`, `RevokedAt`, `ExpiredAt`.
- Add config support for `GATEKEEPER_SESSION_MAX_ACTION_COUNT` with fallback `10`.
- Treat invalid or non-positive config values defensively, falling back to `10`.
- Copy `MaxActionCount` into the session when approval creates it.
- Update EF entity mapping, migration, and model snapshot.

### Slice 2 — Lifecycle application services and endpoints

- Add `POST /api/v1/sessions/{id}/complete`.
- Add `POST /api/v1/sessions/{id}/revoke`.
- Revoke requires `X-Gatekeeper-Admin-Token`.
- Complete follows the current session-action auth posture and does not introduce new client auth.
- `Active -> Completed` succeeds.
- `Active -> Revoked` succeeds.
- Terminal states return `409 Conflict` for further lifecycle mutations.
- Active but expired sessions are materialized as `Expired` before completing/revoking and then return `409 Conflict`.
- `GET /api/v1/sessions/{id}` materializes expired active sessions.
- Add audit events:
  - `SessionCompleted`
  - `SessionRevoked`
  - `SessionExpired`

### Slice 3 — Action execution hardening

- Before adapter dispatch:
  1. Load session.
  2. Materialize lazy expiry if needed.
  3. Reject terminal states.
  4. Check capability allowlist.
  5. Atomically reserve one action budget slot.
  6. Dispatch adapter only after successful reservation.
- If `ActionCount >= MaxActionCount`:
  - no adapter dispatch.
  - no further budget consumption.
  - return `409 Conflict`.
  - write `ActionCountExceeded` audit event.
- Parallel action requests must not exceed `MaxActionCount`.
- Terminal lifecycle transitions must prevent later action reservations.
- Do not keep a database transaction open across adapter I/O.

### Slice 4 — Audit listing API

- Add `GET /api/v1/audit-events`.
- Require `X-Gatekeeper-Admin-Token`.
- Support filters:
  - `aggregateId`
  - `eventType`
  - `from`
  - `to`
- Support cursor/limit pagination.
- Recommended defaults:
  - default limit: `50`
  - max limit: `100`
  - opaque cursor based on `occurredAt` plus `id`.
- Response fields:
  - `id`
  - `eventType`
  - `aggregateId`
  - `occurredAt`
  - bounded `details` or `summary` object.
- Do not expose uncontrolled raw action outputs or arbitrary raw payload as the stable API contract.

### Slice 5 — Documentation and contract cleanup

- Update `docs/current-status.md` after implementation.
- Keep this phase backend-only.
- Add a separate next-phase note for frontend lifecycle/audit wiring.

## Validation Gates

### Backend tests

Run backend tests through host .NET or the Docker SDK fallback:

```bash
dotnet test backend/Gatekeeper.sln
```

If host .NET is unavailable:

```bash
docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Gatekeeper.sln
```

### Formatting and build

```bash
cd backend
dotnet csharpier format .
dotnet csharpier check .
cd ..
dotnet build backend/Gatekeeper.sln --no-restore
```

If host .NET is unavailable, use the Docker SDK fallback documented in the backend workflow.

### Docker validation

```bash
docker compose config
docker compose build backend
```

## Required Test Coverage

- `Active -> Completed` succeeds.
- `Active -> Revoked` succeeds.
- `Active -> Expired` lazy materialization succeeds.
- `Completed`, `Revoked`, and `Expired` are terminal.
- `SessionExpired` is audited once.
- `GATEKEEPER_SESSION_MAX_ACTION_COUNT` fallback is `10`.
- Session creation copies `MaxActionCount`; later config changes do not mutate existing sessions.
- Revoke without or with invalid admin token is rejected.
- Revoke with admin token succeeds.
- Revoke for unknown session returns `404`.
- Revoke for terminal sessions returns `409`.
- Complete active session succeeds.
- Complete for unknown session returns `404`.
- Complete for terminal sessions returns `409`.
- Complete for expired active session materializes expiry and returns `409`.
- GET session materializes expired active session.
- Completed/revoked/expired sessions block adapter dispatch.
- Forbidden capability consumes no budget.
- Invalid or malformed action request consumes no budget.
- Authorized adapter dispatch increments `ActionCount`, including controlled adapter failures.
- Action limit exceeded returns `409`, does not dispatch adapter, and writes `ActionCountExceeded`.
- Parallel actions cannot exceed `MaxActionCount`.
- Audit listing requires admin token.
- Audit filters work for `aggregateId`, `eventType`, `from`, and `to`.
- Audit pagination is stable.
- Audit listing response does not expose uncontrolled raw action outputs or payloads.

## Commit Boundary

This backend phase ends when:

- backend implementation is complete.
- backend tests, formatting, build, and Docker backend build pass.
- spec review passes.
- quality/security review passes.
- `docs/current-status.md` is updated.
- changes are committed and pushed.
