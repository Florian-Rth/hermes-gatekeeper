# Phase 7 — Admin Authentication Hardening

## Status

Planned. Do not implement this phase before this document has been reviewed as the active phase plan.

## Phase Goal

Replace the manually entered static admin token in the web UI with a small local admin login/session flow.

The existing approval, deny, revoke, session lifecycle, and audit browsing flows must remain intact. Admin secrets must no longer be manually pasted into the browser UI or held as a reusable visible token in frontend state.

## Current Baseline

Implemented before this phase:

```text
Access Request -> Approve/Deny in Web UI -> Session -> Execute typed dummy action -> Lifecycle controls in UI -> Audit browsing UI
```

Current admin protection:

- Backend validates `X-Gatekeeper-Admin-Token` against `GATEKEEPER_ADMIN_TOKEN`.
- Token validation uses fixed-time comparison.
- Frontend stores the admin token only in React state, not browser storage.
- Admin-token-protected endpoints are:
  - `POST /api/v1/access-requests/{id}/approve`
  - `POST /api/v1/access-requests/{id}/deny`
  - `POST /api/v1/sessions/{id}/revoke`
  - `GET /api/v1/audit-events`
- `POST /api/v1/sessions/{id}/complete` is currently not admin-token protected and must not be silently changed in this phase.

## Non-Goals

This phase must not add:

- productive target adapters.
- raw shell or break-glass sessions.
- OIDC.
- Passkeys/WebAuthn.
- TOTP.
- mTLS.
- multi-admin approval.
- global session operations console.
- policy DSL.
- broad agent-auth redesign, except where needed to avoid regressions.

## Decision Log

### Decision 1 — Auth model

Chosen: local single-admin authentication for the MVP.

Reason: The roadmap already chooses local admin auth for the MVP and defers OIDC, passkeys, TOTP, mTLS, and multi-admin flows.

Consequence: No external identity provider, no setup wizard, and no admin-user management UI in this phase.

### Decision 2 — Browser session mechanism

Chosen: HttpOnly cookie-based admin session.

Reason: The phase goal is to remove the visible reusable admin token from the browser UI/state. A HttpOnly cookie lets the browser carry the session without exposing the secret to JavaScript.

Consequence: Frontend admin actions use same-origin credentialed requests, not `X-Gatekeeper-Admin-Token` headers.

### Decision 3 — Admin credential source

Chosen: environment-seeded local admin credentials.

Expected configuration:

- `GATEKEEPER_ADMIN_USERNAME`
- `GATEKEEPER_ADMIN_PASSWORD` or, preferably where implementation supports it cleanly, `GATEKEEPER_ADMIN_PASSWORD_HASH`
- optional cookie/session configuration such as:
  - `GATEKEEPER_ADMIN_COOKIE_NAME`
  - `GATEKEEPER_ADMIN_COOKIE_SECURE`
  - `GATEKEEPER_ADMIN_SESSION_IDLE_MINUTES`

Consequence: `.env.example`, Compose docs, README, and startup/config validation must be updated.

### Decision 4 — Password handling

Chosen: no plaintext password persistence.

If an `AdminUser` is persisted, only a password hash may be stored. Password verification must use an established .NET password-hashing mechanism rather than hand-rolled hashing.

Consequence: API responses, audit details, logs, and database fields must not expose plaintext passwords.

### Decision 5 — Cookie security

Chosen: production-safe cookie settings with explicit local-development escape hatch.

Expected cookie attributes:

- `HttpOnly`
- `SameSite=Strict` or at least `Lax`
- `Secure` by default for production-like deployments
- configurable `Secure=false` for localhost HTTP development/Compose demos
- bounded lifetime or idle expiration

Consequence: Development behavior must be documented so local `docker compose` remains usable.

### Decision 6 — CSRF posture

Chosen: treat cookie auth as introducing CSRF risk and add a minimal same-origin/unsafe-request protection.

Acceptable MVP implementation options:

- SameSite Strict plus same-origin checks for unsafe admin requests.
- Or SameSite Strict plus a required non-secret custom header for unsafe admin requests.

Consequence: Backend tests must cover unauthenticated and malformed unsafe admin requests. The implementation must avoid a complex enterprise anti-forgery system unless a simple ASP.NET-supported approach fits cleanly.

### Decision 7 — Admin auth API

Chosen endpoints:

- `POST /api/v1/admin/login`
- `POST /api/v1/admin/logout`
- `GET /api/v1/admin/me`

Consequence: Frontend can login, logout, and rehydrate auth state after reload without storing secrets.

### Decision 8 — Existing admin endpoints

Move these endpoints to the new admin-session boundary:

- `POST /api/v1/access-requests/{id}/approve`
- `POST /api/v1/access-requests/{id}/deny`
- `POST /api/v1/sessions/{id}/revoke`
- `GET /api/v1/audit-events`

Do not silently change `POST /api/v1/sessions/{id}/complete` in this phase.

### Decision 9 — Legacy admin token

Chosen: the visible static token must stop being the normal UI path.

A temporary server-side legacy fallback is acceptable only if it is explicit, documented, and default-safe. The new cookie session must be the primary path validated by tests.

### Decision 10 — Audit

Add bounded audit events:

- `AdminLoginSucceeded`
- `AdminLoginFailed`
- `AdminLogout`

Audit details may include username and reason code. They must not include passwords, cookies, raw headers, or unrestricted client metadata.

### Decision 11 — Login rate limit

Chosen: add a small in-memory login rate limit for MVP hardening.

Reason: A local password login without MFA should still have a basic brute-force brake.

Consequence: No Redis/distributed rate limit in this phase.

### Decision 12 — Frontend auth state

Chosen: replace `AdminTokenProvider` with an admin auth/session provider.

Frontend state may hold only non-sensitive session information, such as authenticated state and username. It must not store the password, cookie, or reusable admin secret in localStorage/sessionStorage.

## Backend Slices

### Backend Slice B1 — Auth contract and config

Goal:

Define the local admin auth contract and configuration.

Expected work:

- Add login/logout/me request and response contracts.
- Add config model/options for admin username, password/hash, cookie, and session settings.
- Decide fail-closed behavior for missing credentials.
- Keep endpoint DTOs out of application services.

TDD behaviors:

- Correct credentials can authenticate.
- Incorrect credentials return unauthorized.
- Missing or invalid admin configuration fails safely with a clear error.
- Auth responses contain no secrets.

### Backend Slice B2 — Password verification and optional AdminUser persistence

Goal:

Provide secure local admin credential verification.

Preferred MVP direction:

- Persist an admin user only if it keeps the model cleaner and testable.
- Store only password hashes.
- Do not add user-management endpoints.

TDD behaviors:

- Seeded/admin credentials work after startup.
- Stored data/API/audit do not expose plaintext password.
- Password hash verification rejects wrong password.

### Backend Slice B3 — Cookie auth and admin endpoints

Goal:

Implement cookie-backed admin login, logout, and me endpoints.

Expected work:

- Configure ASP.NET cookie authentication.
- Set HttpOnly/SameSite/Secure/lifetime attributes.
- Implement:
  - `POST /api/v1/admin/login`
  - `POST /api/v1/admin/logout`
  - `GET /api/v1/admin/me`
- Add minimal CSRF/same-origin protection for unsafe cookie-authenticated admin requests.

TDD behaviors:

- Login sets a HttpOnly SameSite cookie.
- Me returns authenticated admin session with valid cookie.
- Me rejects or reports unauthenticated state without cookie according to documented contract.
- Logout invalidates the cookie.
- Unsafe admin requests without required CSRF/same-origin protection are rejected if the chosen mechanism requires it.

### Backend Slice B4 — Migrate protected admin endpoints

Goal:

Move existing admin operations from manual token header auth to admin-session auth.

Expected work:

- Protect approve, deny, revoke, and audit listing with the new admin auth boundary.
- Preserve existing domain behavior and error mappings.
- Avoid changing session complete unless separately decided.
- Keep request creation and session action execution behavior unchanged.

TDD behaviors:

- Approve/deny/revoke/audit without admin cookie return 401.
- Approve/deny/revoke/audit with logged-in admin cookie work as before.
- Wrong credentials cannot obtain admin access.
- Existing 404/409 validation behaviors remain unchanged.
- Agent/request/session action paths do not regress.

### Backend Slice B5 — Audit and login rate limit

Goal:

Audit admin auth events and add minimal brute-force protection.

Expected work:

- Add `AdminLoginSucceeded`, `AdminLoginFailed`, and `AdminLogout` audit events.
- Add bounded details only.
- Add simple in-memory login rate limiting.

TDD behaviors:

- Successful login writes bounded audit event.
- Failed login writes bounded audit event.
- Logout writes bounded audit event.
- Audit payloads contain no passwords/cookies.
- Repeated failed login attempts are rate-limited with documented status.

## Frontend Slices

### Frontend Slice F1 — Auth API boundary

Goal:

Create the frontend admin-auth API layer.

Expected work:

- Add `frontend/src/features/admin-auth`.
- Add Zod schemas and inferred types.
- Add TanStack Query hooks for login, logout, and me/session.
- Ensure same-origin credentialed requests are used where required.
- Remove token header handling from new admin-auth calls.

Tests:

- Login sends username/password to `/api/v1/admin/login`.
- Responses are validated via Zod.
- Logout calls `/api/v1/admin/logout`.
- No secrets are persisted to browser storage.

### Frontend Slice F2 — Auth provider and login UI

Goal:

Replace manual token entry with a local admin login flow.

Expected work:

- Replace `AdminTokenProvider` with `AdminAuthProvider` or equivalent.
- Add login UI/page/panel with username and password.
- Add initial `/api/v1/admin/me` session check.
- Add logout affordance in the app shell/header.
- Gate dashboard/admin actions behind authenticated state.

Tests:

- Unauthenticated user sees login UI.
- Successful login shows the dashboard.
- Failed login shows a useful error.
- Logout returns to login state.
- Password is not retained outside the password input flow.

### Frontend Slice F3 — Migrate existing admin flows

Goal:

Remove visible token usage from approval, revoke, and audit UI.

Expected work:

- Remove `AdminTokenPanel` from the normal UI.
- Update approve/deny/revoke/audit hooks to rely on cookie auth.
- Remove `adminToken` props and token-version query-key patterns where obsolete.
- Add session-expired handling for 401s.

Tests:

- Approve sends no `X-Gatekeeper-Admin-Token` header.
- Deny sends no `X-Gatekeeper-Admin-Token` header.
- Revoke sends no `X-Gatekeeper-Admin-Token` header.
- Audit loads only when authenticated.
- 401 produces login/session-expired state.

### Frontend Slice F4 — UX polish inside scope

Goal:

Make the auth flow understandable without adding new product scope.

Expected work:

- Clear copy for local admin login.
- Clear session-expired message.
- No new adapter UI.
- No global session console.

Tests:

- User-visible labels and error messages are accessible.
- Login/logout controls have accessible names.

## Validation Gates

### Backend gates

Run from repository root or backend directory as appropriate:

```bash
cd backend && dotnet csharpier check .
cd backend && dotnet build Gatekeeper.sln --no-restore
cd backend && dotnet test Gatekeeper.sln --no-restore
docker compose config
docker compose build backend
```

If the host lacks the required .NET SDK, use the documented .NET SDK Docker fallback from the backend skill/project history.

### Frontend gates

```bash
cd frontend && pnpm check
cd frontend && pnpm test -- --run
cd frontend && pnpm build
docker compose config
docker compose build frontend
```

### Full phase smoke gate

Demonstrate the browser/API flow:

1. Start backend and frontend.
2. Login as local admin.
3. Create an access request.
4. Approve or deny request in UI without entering an admin token.
5. For approved request, view session lifecycle and revoke from UI.
6. View audit feed without entering an admin token.
7. Logout.
8. Verify admin actions no longer work after logout.

### Security review gate

Verify:

- No admin token field in normal UI.
- No admin password/session secret in localStorage/sessionStorage.
- Cookie is HttpOnly and SameSite.
- Production cookie secure behavior is documented/configured.
- Audit events contain no passwords or cookies.
- Legacy admin token behavior is removed or explicitly documented as transitional/default-safe.

## Implementation and Commit Boundary

This phase should be implemented with fresh subagents and backend/frontend separation.

Recommended sequence:

1. Backend implementation subagent:
   - Slices B1-B5.
   - TDD required.
   - No frontend changes except API contract notes if unavoidable.
2. Backend spec review subagent.
3. Backend quality/security review subagent.
4. Frontend implementation subagent:
   - Slices F1-F4.
   - No backend behavior changes.
5. Frontend spec review subagent.
6. Frontend quality/UX/security review subagent.
7. Orchestrator final validation and docs update.

Preferred commit boundaries:

- `feat: add local admin cookie authentication`
- `feat: replace admin token UI with login session`
- `docs: record admin authentication hardening`

If a single final phase commit is preferred, internal work must still be separated and fully validated before commit.

## Documentation Updates Required After Implementation

Update:

- `docs/current-status.md`
- `docs/implementation-plan.md`
- `docs/decisions.md`
- `README.md`
- `.env.example`

Potentially add:

- `docs/security.md` if auth/session configuration becomes too large for README.

## Known Pre-Implementation Notes

- README currently lags behind `docs/current-status.md` for Phase 5/6 status and should be corrected during this phase.
- The complete-session endpoint is intentionally not moved into admin auth without explicit follow-up decision.
- Cookie auth introduces CSRF considerations; the chosen minimal protection must be documented and tested.
