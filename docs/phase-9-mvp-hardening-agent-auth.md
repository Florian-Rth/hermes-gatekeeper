# Phase 9 Implementation Plan — MVP Hardening and Agent API Authentication

> **For Hermes:** Use `subagent-driven-development` to implement this plan task-by-task. Use fresh subagents per slice, with spec review first and code quality/security review second.

**Goal:** Harden the MVP by requiring configured Agent API keys for access-request creation and session-action execution, and by attributing those actions in audit logs.

**Architecture:** Keep Admin Cookie Auth and Agent API Auth as separate trust boundaries. The API layer authenticates the `X-Gatekeeper-Agent-Key` header, resolves a non-secret agent identity, maps that identity into application commands, and writes bounded audit events. Application/Core must not depend on `HttpContext`, raw headers, or secret material.

**Tech Stack:** C#/.NET 10, ASP.NET Core/FastEndpoints, Clean Architecture, EF Core/SQLite, xUnit v3, ASP.NET Options, Docker Compose.

---

## Binding Phase Decisions

### Decision 1 — Phase boundary

Chosen: Phase 9 is backend-only MVP hardening focused on Agent API Authentication.

Consequence:

- No frontend work in this phase.
- Admin Login/Cookie Auth stays unchanged for browser/admin operations.
- Agent Auth protects only machine/agent-facing operations.

### Decision 2 — Authentication model

Chosen: Static server-configured Agent API keys for the MVP.

Consequence:

- No OIDC, mTLS, TOTP, Passkeys, WebAuthn, OAuth device flow, or multi-admin approval in Phase 9.
- No Agent Key management UI or DB-backed agent registry in Phase 9.
- Rotation is a deployment/configuration operation for now.

### Decision 3 — Header contract

Chosen: Agents authenticate with:

```http
X-Gatekeeper-Agent-Key: <secret>
```

Consequence:

- Agent keys are never sent through cookies.
- Admin cookies do not authorize agent endpoints.
- Agent keys do not authorize admin endpoints.

### Decision 4 — Agent identity

Chosen: Each configured key maps to a non-secret `agentId` plus `authMethod=apiKey`.

Example configuration shape:

```text
GATEKEEPER_AGENT_AUTH_ENABLED=true
GATEKEEPER_AGENT_KEYS__0__ID=hermes-local
GATEKEEPER_AGENT_KEYS__0__KEY=<secret-from-deployment-secret-store>
GATEKEEPER_AGENT_KEYS__1__ID=ci-smoke
GATEKEEPER_AGENT_KEYS__1__KEY=<another-secret>
```

Consequence:

- Audit events can say which agent acted.
- Audit events and responses must never contain the API key.

### Decision 5 — Authorization scope

Chosen: Phase 9 uses coarse Agent Auth only. A valid agent key may create access requests and execute actions against an already-approved session. Fine-grained per-agent target/capability scopes are intentionally deferred.

Consequence:

- Target/action safety is still enforced by the existing approval/session/profile/SSH-policy chain.
- Later phases may add per-agent target/profile scopes if needed.

### Decision 6 — Failure posture

Chosen: Fail closed.

Consequence:

- Missing, blank, malformed, or invalid API keys return `401 Unauthorized`.
- If Agent Auth is enabled but no valid keys are configured, the protected endpoints must not become open.
- Startup/options validation is preferred where practical; otherwise the guard returns a deterministic auth/config failure without executing endpoint behavior.

### Decision 7 — Audit posture

Chosen: Successful and failed Agent Auth paths are audit-visible, but bounded.

Consequence:

- Successful `AccessRequestCreated` and session-action audit events include `agentId` and `authMethod`.
- Failed auth attempts write a bounded event such as `AgentAuthenticationFailed` with safe metadata only.
- Never audit raw headers, API keys, cookies, request bodies, SSH credentials, or unbounded action output.

### Decision 8 — No new capabilities

Chosen: Phase 9 does not add new target capabilities.

Consequence:

- No raw shell.
- No sudo.
- No write actions.
- No TTY/interactivity.
- No file upload/download.
- No port forwarding.
- No production/HomeLab integration.
- No Home Assistant, Docker, Proxmox, Kubernetes, or HTTP-service connector.

## Public API Contract

### Protected agent endpoints

After this phase, these endpoints require a valid Agent API key:

```text
POST /api/v1/access-requests
POST /api/v1/sessions/{sessionId}/actions
```

### Header

```http
X-Gatekeeper-Agent-Key: <secret>
```

### Expected responses

- `401 Unauthorized` for missing key.
- `401 Unauthorized` for blank/malformed key.
- `401 Unauthorized` for invalid key.
- Existing validation and domain result mappings remain unchanged after successful authentication.

### Explicit boundary tests

- Admin cookie alone does not authorize protected agent endpoints.
- Agent key does not authorize admin endpoints such as approve, deny, revoke, or audit listing.

## Audit Contract

### Successful agent activity

The following audit paths should include `agentId` and `authMethod` when the request authenticated as an agent:

- `AccessRequestCreated`
- `SessionActionRequested`
- `SessionActionAllowed`
- `SessionActionDenied`
- `SessionActionExecuted`
- `SessionActionFailed`
- action-budget/lifecycle denial events when they happen in an authenticated agent request context

### Failed agent authentication

Add a bounded audit event, recommended name:

```text
AgentAuthenticationFailed
```

Safe details:

- endpoint identifier or route template
- HTTP method
- reason code, e.g. `missing_key`, `invalid_key`, `malformed_key`, `auth_not_configured`
- `authMethod=apiKey`

Forbidden details:

- API key
- raw headers
- cookies
- request body
- SSH credentials
- action output
- stack traces or library exception details

## Non-Goals

- No frontend changes.
- No OIDC/mTLS/TOTP/Passkeys/WebAuthn/OAuth.
- No multi-admin approval.
- No Agent Key UI.
- No DB-backed Agent Registry.
- No per-agent target/profile/action scopes.
- No rate limiting as a required first cut.
- No production/HomeLab connector onboarding.
- No additional special-purpose connectors.
- No raw shell, sudo, write actions, TTY, file transfer, or port forwarding.

## Implementation Slices

## Task A1 — Agent Auth configuration and options

**Objective:** Add configuration binding and validation for Agent API Authentication without changing endpoint behavior yet.

**Scope:** Backend only. No endpoint protection yet. No DB changes.

**Files to inspect first:**

- `backend/src/Gatekeeper.Api/Program.cs`
- `backend/src/Gatekeeper.Api/AdminAuthentication/AdminAuthOptions.cs`
- `backend/src/Gatekeeper.Api/AdminAuthentication/AdminAuthConstants.cs`
- `backend/tests/Gatekeeper.Tests/AdminAuthenticationEndpointTests.cs`
- existing options/config tests if present

**Expected files to add/modify:**

- Create `backend/src/Gatekeeper.Api/AgentAuthentication/AgentAuthOptions.cs`
- Create `backend/src/Gatekeeper.Api/AgentAuthentication/AgentApiKeyOptions.cs` if useful
- Create `backend/src/Gatekeeper.Api/AgentAuthentication/AgentAuthConstants.cs`
- Modify `backend/src/Gatekeeper.Api/Program.cs` or the existing DI/config registration location
- Add tests under `backend/tests/Gatekeeper.Tests`

**Behaviors to test with TDD:**

1. Valid config with one agent key binds successfully.
2. Valid config with multiple agent keys binds and preserves distinct IDs.
3. Duplicate agent IDs are rejected.
4. Blank agent IDs are rejected.
5. Blank agent keys are rejected.
6. Agent Auth enabled with no valid keys fails closed.
7. The options model does not expose key values through any response/audit path introduced in this task.

**Validation commands:**

```bash
docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Gatekeeper.sln

docker run --rm -u $(id -u):$(id -g) -e DOTNET_CLI_HOME=/tmp -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet tool restore --verbosity quiet >/dev/null && dotnet tool run csharpier -- check src tests'
```

**Commit boundary:** Do not commit A1 alone unless the implementation is reviewed and all targeted validation passes. Prefer committing Phase 9 after A1-A6 if the branch remains manageable.

## Task A2 — Agent key verifier and identity model

**Objective:** Implement a reusable fixed-time key verifier that resolves an authenticated agent identity.

**Scope:** Backend only. No endpoint protection yet.

**Expected files to add/modify:**

- Create `backend/src/Gatekeeper.Api/AgentAuthentication/AgentIdentity.cs`
- Create `backend/src/Gatekeeper.Api/AgentAuthentication/AgentAuthResult.cs` or equivalent
- Create `backend/src/Gatekeeper.Api/AgentAuthentication/AgentApiKeyVerifier.cs`
- Add verifier tests under `backend/tests/Gatekeeper.Tests`

**Behavior tests:**

1. A correct key resolves the configured `agentId` and `authMethod=apiKey`.
2. An unknown key is rejected with reason `invalid_key`.
3. A missing/blank key is rejected with reason `missing_key` or `malformed_key`.
4. Verification uses fixed-time comparison semantics; do not use simple `==` or case-insensitive matching for secrets.
5. Failure results contain no secret material.
6. Agent ID matching is exact and non-secret.

**Implementation notes:**

- Keep HTTP header extraction out of the verifier.
- Use traditional constructors and `private readonly` fields.
- Keep classes `sealed` unless intentionally inherited.
- Use typed reason codes rather than exceptions for expected failures.

**Validation commands:** Same as A1.

## Task A3 — Guard protected agent endpoints

**Objective:** Require valid Agent API keys for request creation and session-action execution.

**Scope:** API/backend only.

**Files to inspect first:**

- `backend/src/Gatekeeper.Api/Endpoints/AccessRequests/CreateAccessRequestEndpoint.cs`
- `backend/src/Gatekeeper.Api/Endpoints/Sessions/ExecuteSessionActionEndpoint.cs`
- `backend/src/Gatekeeper.Api/AdminAuthentication/AdminSessionGuard.cs`
- existing endpoint tests for access request creation and session actions

**Expected files to add/modify:**

- Create `backend/src/Gatekeeper.Api/AgentAuthentication/AgentApiKeyGuard.cs`
- Modify `CreateAccessRequestEndpoint.cs`
- Modify `ExecuteSessionActionEndpoint.cs`
- Update integration tests that call these endpoints

**Behavior tests:**

1. `POST /api/v1/access-requests` without agent key returns `401 Unauthorized`.
2. `POST /api/v1/access-requests` with invalid agent key returns `401 Unauthorized`.
3. `POST /api/v1/access-requests` with valid agent key preserves the existing success behavior.
4. `POST /api/v1/sessions/{sessionId}/actions` without agent key returns `401 Unauthorized`.
5. `POST /api/v1/sessions/{sessionId}/actions` with invalid agent key returns `401 Unauthorized`.
6. `POST /api/v1/sessions/{sessionId}/actions` with valid agent key preserves existing success and failure mappings.
7. Admin cookie alone does not authorize these agent endpoints.
8. Agent key does not authorize approve, deny, revoke, or audit-listing endpoints.

**Implementation notes:**

- Return minimal non-leaky unauthorized responses.
- Do not execute endpoint validation/application logic after auth failure.
- Keep admin and agent auth boundaries separate.

**Validation commands:** Same as A1, plus any targeted endpoint test commands the implementer uses during TDD.

## Task A4 — Propagate agent identity into application commands and successful audit

**Objective:** Make successful agent identity visible in application/audit events without coupling Application to HTTP concerns.

**Files to inspect first:**

- `backend/src/Gatekeeper.Application/AccessRequests/CreateAccessRequestCommand.cs`
- `backend/src/Gatekeeper.Application/AccessRequests/AccessRequestService.cs`
- `backend/src/Gatekeeper.Application/Sessions/ExecuteSessionActionCommand.cs`
- `backend/src/Gatekeeper.Application/Sessions/SessionActionService.cs`
- audit event factories/domain model under `backend/src/Gatekeeper.Core`
- audit repository/service under `backend/src/Gatekeeper.Infrastructure`

**Expected files to add/modify:**

- Add an application-layer non-secret agent actor/identity value if needed.
- Extend `CreateAccessRequestCommand` with agent actor data.
- Extend `ExecuteSessionActionCommand` with agent actor data.
- Update endpoint manual mapping.
- Enrich safe audit details.
- Update tests.

**Behavior tests:**

1. `AccessRequestCreated` audit includes `agentId` and `authMethod`.
2. `SessionActionRequested` audit includes `agentId` and `authMethod`.
3. Successful action audit chain includes agent identity where relevant.
4. Denied action audit includes agent identity when auth succeeded but session/policy denied execution.
5. Audit details never include the API key or raw header.
6. Existing SSH audit metadata remains present after adding agent metadata.

**Implementation notes:**

- Do not put `HttpContext`, headers, cookies, or API DTOs into Application.
- Prefer a small application-owned record/value for the authenticated actor.
- Manual mapping only.

**Validation commands:** Same as A1.

## Task A5 — Audit failed agent authentication attempts

**Objective:** Record bounded audit events when protected agent endpoints reject requests before application execution.

**Scope:** Backend only. No request body persistence. No rate limiting.

**Expected files to add/modify:**

- Add audit event factory or writer for `AgentAuthenticationFailed`.
- Add API-level auth failure audit writer if needed.
- Integrate with `AgentApiKeyGuard`.
- Update tests.

**Behavior tests:**

1. Missing key on access request creation writes `AgentAuthenticationFailed` with reason `missing_key`.
2. Invalid key on access request creation writes `AgentAuthenticationFailed` with reason `invalid_key`.
3. Missing key on session action execution writes `AgentAuthenticationFailed` with reason `missing_key`.
4. Invalid key on session action execution writes `AgentAuthenticationFailed` with reason `invalid_key`.
5. Failed auth audit contains no API key, raw headers, cookies, or request body.
6. Valid auth does not write a failed-auth audit event.

**Implementation notes:**

- Use a route template or endpoint identifier instead of unbounded raw path where practical.
- Keep audit payload small.
- Avoid introducing dependencies from Core/Application back to API.

**Validation commands:** Same as A1.

## Task A6 — Demo config, docs, and end-to-end smoke validation

**Objective:** Update local/demo configuration and docs so the Phase 8 Compose SSH demo still works with Agent Auth enabled.

**Files to inspect first:**

- `README.md`
- `compose.yml`
- `.env.example` if present
- `backend/src/Gatekeeper.Api/appsettings.Development.json`
- `docs/current-status.md`
- `docs/phase-8-compose-ssh-demo.md`

**Expected files to add/modify:**

- Add demo-only Agent Auth config to development/compose configuration.
- Add safe example docs with placeholders or clearly demo-only values.
- Update curl examples to pass `X-Gatekeeper-Agent-Key` via environment variable.
- Update `docs/current-status.md` after implementation.
- Update `README.md` status and docs links if needed.

**Behavior/E2E validation:**

1. Start Compose stack with demo Agent Key configured.
2. `POST /api/v1/access-requests` without key returns `401`.
3. `POST /api/v1/access-requests` with key succeeds.
4. Admin login approves the request.
5. `POST /api/v1/sessions/{id}/actions` without key returns `401`.
6. `POST /api/v1/sessions/{id}/actions` with key succeeds against `demo-ssh` using `system.status.read`.
7. Audit listing shows safe failed-auth events and successful events with `agentId/authMethod`.
8. No API key appears in audit responses.

**Final validation gate:**

```bash
git diff --check

docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet restore Gatekeeper.sln && dotnet build Gatekeeper.sln --no-restore && dotnet test Gatekeeper.sln --no-build'

docker run --rm -u $(id -u):$(id -g) -e DOTNET_CLI_HOME=/tmp -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet tool restore --verbosity quiet >/dev/null && dotnet tool run csharpier -- check src tests'

docker compose config

docker compose build backend
```

If any frontend files are changed unexpectedly, also run:

```bash
pnpm --dir frontend check
pnpm --dir frontend test -- --run
pnpm --dir frontend build
```

## Optional Later Hardening — Not Required for Phase 9

These are intentionally not part of the required Phase 9 scope:

- per-agent target/capability/action scopes
- rate limiting / invalid-key throttling
- key hashing at rest in config
- DB-backed agent registry
- Agent Key UI
- OIDC/mTLS/OAuth device flow
- production HomeLab target onboarding

They may become Phase 10+ work after the backend-only Agent Auth boundary is implemented and validated.

## Open Questions

None blocking.

The optional future question is whether agent keys should later get per-target/per-capability scopes. Do not implement that in Phase 9 unless Florian explicitly changes the phase boundary.

## Commit and Push Boundary

Phase 9 is complete only when:

1. A1-A6 are implemented with TDD-style vertical slices.
2. Spec compliance review passes.
3. Code quality/security review approves.
4. Full backend validation passes.
5. Compose config/build and demo smoke validation pass.
6. `docs/current-status.md` and relevant user-facing docs are updated.
7. Changes are committed and pushed to `main`.
