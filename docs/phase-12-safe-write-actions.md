# Phase 12 Implementation Plan — Safe Write Actions

> **For Hermes:** Use `subagent-driven-development` to implement this plan task-by-task. Use fresh subagents per slice, with spec review first and code quality/security review second.

**Goal:** Add the first real mutating maintenance actions to Gatekeeper without breaking the typed approval/session/audit model.

**Architecture:** Extend the existing SSH target/profile/action model instead of introducing a second execution path. Mutating actions must stay server-side configured, separately profiled, tightly allowlisted, and explicitly visible in audit/results.

**Tech Stack:** C#/.NET 10, ASP.NET Core/FastEndpoints, Clean Architecture, EF Core/SQLite, xUnit v3, ASP.NET Options, Docker Compose, SSH.NET-based connector.

---

## Binding Phase Decisions

### Decision 1 — Product priority

Chosen: After the existing read-only product kernel, the next concrete phase is real product functionality, not another generic hardening detour.

Consequence:

- The first safe write-action set is prioritized now.
- The existing read-only connector remains the baseline, not the finish line.

### Decision 2 — Execution model

Chosen: Reuse the existing typed session-action endpoint and SSH connector path.

Consequence:

- No raw shell endpoint.
- No separate write-only transport or backdoor.
- Agent requests remain `target` + `action` + typed `parameters`.

### Decision 3 — First action set

Chosen:

- `service.restart`
- `service.reload`
- `backup.trigger`
- `container.restart` only on targets that explicitly support and configure it

Consequence:

- The first mutating scope stays small and maintenance-focused.
- Generic file write/patch actions remain out of scope.

### Decision 4 — Separate maintenance profile

Chosen: Mutating actions require a separate server-side profile, for example `remote.maintenance.basic`.

Consequence:

- `remote.readonly.inspect` remains read-only.
- Approvals can explicitly distinguish read-only vs. maintenance authority.

### Decision 5 — Risk posture

Chosen: The first write-actions are high-visibility, explicitly audited maintenance actions, not broad editing primitives.

Consequence:

- Add explicit action metadata in configuration, at minimum whether an action is mutating and its risk label.
- Audit/results must clearly show mutating intent and outcome.

## Supported Actions to Document

Documentation should stay short and operational:

### Already supported today

Dummy:

- `test.echo`
- `test.status.read`
- `test.fail`

SSH read-only (`demo-ssh`, `remote.readonly.inspect`):

- `system.status.read`
- `disk.usage.read`
- `service.status.read` with allowlisted `service=sshd`

### Planned in this phase

Maintenance / safe write:

- `service.restart`
- `service.reload`
- `backup.trigger`
- `container.restart` on explicitly supported targets only

## Public API Contract

No new public endpoint. Continue using:

```text
POST /api/v1/sessions/{sessionId}/actions
```

Mutating action request examples:

```json
{
  "target": "demo-ssh",
  "action": "service.restart",
  "parameters": {
    "service": "demo-app"
  }
}
```

```json
{
  "target": "demo-ssh",
  "action": "backup.trigger",
  "parameters": {
    "job": "nightly-config"
  }
}
```

## Scope

### In scope

- Extend the existing SSH action configuration model with explicit mutating-action metadata.
- Add a separate maintenance profile.
- Implement the first write-action set as tightly allowlisted named actions.
- Distinguish mutating result/audit semantics from pure read-only output.
- Update README and `docs/current-status.md` with a short supported-actions section.

### Out of scope

- Raw shell.
- Sudo.
- Generic file write or patch operations.
- File transfer.
- Port forwarding.
- Automated onboarding of productive targets.
- A broad policy DSL.
- OIDC/mTLS/Passkeys/TOTP/Multi-Admin approval.

## Suggested Implementation Slices

### Slice W1 — Action metadata and maintenance profile model

Goal: Extend the server-side action configuration so mutating actions are explicit and separable from read-only actions.

Note: W1 is only groundwork. It does not yet make mutating actions executable or visible in final audit/result payloads; those belong to the following vertical slices.

Expected tests:

- Read-only profile does not authorize mutating actions.
- Maintenance profile authorizes only its configured actions.
- Missing mutating metadata or invalid combinations fail safely.

### Slice W2 — `service.restart` vertical slice

Goal: First end-to-end mutating action through the existing approval/session/action flow.

Binding W2 decisions:

- `service.restart` stays on the existing typed session-action endpoint; no new public API.
- Authorization uses a separate maintenance profile: `remote.maintenance.basic`.
- `remote.readonly.inspect` must not authorize `service.restart`.
- Parameters stay strictly allowlisted with exactly one string parameter: `service`.
- The development/demo target uses a dedicated demo service name such as `demo-app`, not `sshd`, so the running SSH connection is not destabilized by the restart demo.
- The demo implementation may realize the restart effect through the existing forced-command wrapper as long as the Gatekeeper side still models it as a normal typed SSH action.
- W2 must make mutating intent visible in both action result payloads and audit details with explicit `isMutating=true` and `risk=High` markers.

Expected tests:

- Approved maintenance profile can execute `service.restart` for an allowlisted service.
- Read-only profile cannot execute `service.restart`.
- Invalid/non-allowlisted service is denied.
- Audit/result clearly mark the action as mutating.

### Slice W3 — `service.reload` vertical slice

Goal: Add a lower-impact sibling action with the same policy model.

Expected tests:

- Same authorization/allowlist rules as restart.
- Separate action name and audit trail.

### Slice W4 — `backup.trigger` vertical slice

Goal: Add a non-service maintenance action that still fits the same typed model.

Expected tests:

- Only allowlisted backup jobs may be triggered.
- Result/audit remain bounded and structured.

### Slice W5 — `container.restart` target-specific support

Goal: Add this action only where a target explicitly configures it.

Expected tests:

- Unsupported target/profile combinations deny safely.
- Explicitly configured target can execute the action.

### Slice W6 — Demo/doc updates

Goal: Keep docs short and truthful about what is currently supported.

Expected outputs:

- README short supported-actions section updated.
- `docs/current-status.md` canonical supported-actions section updated.
- Compose/demo docs updated if the local demo target gains a safe-write action.

## Validation Gates

Backend:

```bash
docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet restore Gatekeeper.sln && dotnet build Gatekeeper.sln --no-restore && dotnet test Gatekeeper.sln --no-build'

docker run --rm -u $(id -u):$(id -g) -e DOTNET_CLI_HOME=/tmp -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet tool restore --verbosity quiet >/dev/null && dotnet tool run csharpier -- check src tests'
```

Compose/demo:

```bash
docker compose config
docker compose build
```

Manual smoke target for the first write slice:

```text
Agent request -> Admin approve maintenance profile -> Execute write action -> Verify effect -> Audit
```

## Commit Boundary

Phase 12 is complete only when:

- At least one real write-action is implemented end-to-end.
- The first action set is documented briefly and truthfully.
- Reviews approve the design and code quality/security.
- Validation passes and the changes are committed and pushed.
