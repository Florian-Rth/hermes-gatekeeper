# Phase 6 — Session Lifecycle and Audit Visibility UI

> **For Hermes:** Use `florian-ai-coding-workflow`, `florian-frontend-work`, `florian-ux-expert`, `florian-tdd`, and `subagent-driven-development`. Keep the phase frontend-only and implement in small reviewed slices.

## Goal

Make the Phase 5 backend lifecycle and audit capabilities visible and operable in the web UI.

Admins should be able to understand a known session's lifecycle state, see action budget usage, complete or revoke active sessions, and inspect relevant audit events without introducing new backend scope.

## Grill-Me Decisions

### 1. Phase boundary

Phase 6 is strictly frontend-only.

It must not add backend endpoints, migrations, application services, domain changes, or API contract changes. If a desired UI requires a missing backend contract, document it as a follow-up instead of building it in this phase.

### 2. Session discovery

Phase 6 does not introduce or assume a global session list endpoint.

There is no `GET /api/v1/sessions` in scope. The UI works only with known session IDs from existing flows:

- the approval response,
- selected request/session detail data when available,
- audit context/filtering when a session or aggregate id is known.

A global session operations console is a later backend/API phase.

### 3. User job

The primary admin job is:

1. review an access request,
2. approve or deny it,
3. understand the resulting session,
4. stop or complete that session when appropriate,
5. verify what happened through audit events.

The existing request review flow remains the center of the UI. Session lifecycle and audit visibility extend it; they do not replace it.

### 4. Lifecycle actions

- `Revoke` is a safety/security action and requires the existing admin token header.
- `Complete` is a normal session-finished action and follows the existing backend contract without adding new frontend-side auth requirements.
- Both actions should be disabled or hidden for terminal sessions when the status is known.
- Revoke should use a confirmation step because it blocks further actions immediately.
- Complete can use a lighter confirmation or direct action, but must still show pending/success/error states clearly.

### 5. Admin token handling

Keep the Phase 4 token model:

- Admin token is held only in React state.
- Do not store it in `localStorage`, `sessionStorage`, cookies, IndexedDB, URL params, logs, or test snapshots.
- Send it only as `X-Gatekeeper-Admin-Token` to admin-protected endpoints.
- Reuse one shared in-memory token state for approve, deny, revoke, and audit listing.

### 6. Audit visibility

Audit visibility is a minimal admin feed, not a full observability product.

The UI should support the existing backend filters:

- `aggregateId`,
- `eventType`,
- `from`,
- `to`,
- cursor/limit pagination.

Audit event details should be bounded and readable. Do not make raw arbitrary payload/output display the primary UX.

### 7. Compound composition strategy

Use compound composition wherever it creates clearer UI boundaries and avoids prop drilling.

Strong candidates:

```text
SessionLifecycleCard.Root
SessionLifecycleCard.Header
SessionLifecycleCard.StatusBadge
SessionLifecycleCard.Budget
SessionLifecycleCard.Capabilities
SessionLifecycleCard.Timestamps
SessionLifecycleCard.Actions
```

```text
AuditFeed.Root
AuditFeed.Filters
AuditFeed.List
AuditFeed.Item
AuditFeed.Details
AuditFeed.Pagination
AuditFeed.EmptyState
AuditFeed.ErrorState
```

Potential shared dashboard composition:

```text
GatekeeperDashboard.Root
GatekeeperDashboard.Sidebar
GatekeeperDashboard.Main
GatekeeperDashboard.Section
```

Do not force compound composition onto trivial one-off utilities or pure formatting components.

### 8. UX states

Every new data-driven UI area needs explicit states:

- loading,
- empty,
- success,
- disabled/pending mutation,
- error,
- terminal/read-only.

Error messages should be action-oriented:

- `401`/`403` on admin APIs: tell the admin to check the token.
- `404` session: session was not found or is unavailable.
- `409` lifecycle mutation: session is already completed, revoked, expired, or otherwise no longer mutable.
- network failure: backend is unreachable.
- schema failure: frontend/backend contract mismatch.

## In Scope

- Extend frontend Zod schemas for Phase 5 session lifecycle fields.
- Add frontend API/query hooks for:
  - session details,
  - session complete,
  - session revoke,
  - audit events listing/filtering/pagination.
- Replace or extend the existing minimal session summary with a compound `SessionLifecycleCard`.
- Add lifecycle action UI for active sessions.
- Add an `AuditFeed` compound component with filters and cursor pagination.
- Wire audit visibility into the existing approval dashboard/request flow.
- Reuse existing admin token state; refactor it into a shared in-memory context if needed.
- Add user-visible loading, empty, error, success, and terminal states.
- Add frontend tests for the main user-visible flows and security-sensitive token handling.

## Non-goals

- No backend implementation.
- No `GET /api/v1/sessions`.
- No global session list or session operations console.
- No new audit filters beyond the existing backend API.
- No API contract changes.
- No database or migration work.
- No full admin login.
- No cookie auth.
- No OIDC, TOTP, Passkeys, mTLS, or multi-admin approval.
- No Hermes client-token hardening.
- No productive adapters.
- No raw shell.
- No policy UI.
- No audit export feature.
- No tamper-proof audit hash chain.
- No broad redesign or branding phase.

## API Contracts Used

Existing access request APIs:

- `GET /api/v1/access-requests`
- `GET /api/v1/access-requests/{id}`
- `POST /api/v1/access-requests/{id}/approve`
- `POST /api/v1/access-requests/{id}/deny`

Existing session APIs:

- `GET /api/v1/sessions/{id}`
- `POST /api/v1/sessions/{id}/actions`
- `POST /api/v1/sessions/{id}/complete`
- `POST /api/v1/sessions/{id}/revoke`

Existing audit API:

- `GET /api/v1/audit-events`

Admin token header where required:

```http
X-Gatekeeper-Admin-Token: <token>
```

## Implementation Slices

### Slice 1 — API boundary and types

- Extend or add Zod schemas for session details including lifecycle and budget fields.
- Add Zod schemas for audit event list responses.
- Add TanStack Query hooks for session details, complete, revoke, and audit events.
- Keep all API calls inside feature `api.ts` files.
- Validate all API responses at the boundary.

### Slice 2 — Shared admin token state

- Extract the existing token state into a small in-memory context or hook if the current component-local state would cause prop drilling.
- Keep token storage ephemeral.
- Ensure approve, deny, revoke, and audit listing can share the same token without persisting it.

### Slice 3 — SessionLifecycleCard compound component

- Build a compound `SessionLifecycleCard` with context-backed slots.
- Show status, timestamps, allowed targets, allowed capabilities, and action budget.
- Provide complete/revoke actions for active sessions.
- Disable lifecycle actions for terminal sessions.
- Show clear success/error states after mutations.

### Slice 4 — AuditFeed compound component

- Build a compound `AuditFeed` with filters, list, event item/details, pagination, empty and error states.
- Support existing backend filters: aggregateId, eventType, from, to.
- Use cursor pagination and a bounded limit.
- Use readable event details rather than uncontrolled raw payload dumps.

### Slice 5 — Dashboard integration

- Integrate the session lifecycle card into the existing access request dashboard.
- Preserve the current review/approve/deny flow.
- Show audit feed in the context of the selected request/session where possible.
- Keep the optional dummy action UI secondary to lifecycle and audit controls.

### Slice 6 — Tests and validation

Required frontend tests:

- Existing approval dashboard still renders and loads requests/details.
- Session lifecycle card shows status, budget, capabilities, and timestamps.
- Revoke sends `X-Gatekeeper-Admin-Token` and updates/refetches UI.
- Revoke without token shows or prevents with an actionable token error.
- Complete calls the existing complete endpoint and updates/refetches UI.
- Terminal session disables invalid lifecycle actions.
- Audit feed loads with admin token.
- Audit filters are encoded into the request query.
- Audit pagination follows the next cursor.
- Empty audit result shows a useful empty state.
- Admin token is not written to browser storage.

Validation commands:

```bash
cd frontend && pnpm check
cd frontend && pnpm test -- --run
cd frontend && pnpm build
docker compose config
docker compose build frontend
```

## Commit Boundary

Phase 6 ends when:

- the frontend-only scope is preserved,
- the dashboard exposes session lifecycle and audit visibility,
- compound composition is used for the main session and audit UI groups,
- required frontend tests pass,
- frontend build/check pass,
- Docker frontend validation passes,
- docs are updated,
- changes are reviewed, committed, and pushed.
