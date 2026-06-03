# Phase 12 Implementation Plan — Safe Write Actions

> **For Hermes:** Use `subagent-driven-development` to implement this plan task-by-task. Use fresh subagents per slice, with spec review first and code quality/security review second.

**Goal:** Add the first real mutating maintenance actions to Gatekeeper without breaking the typed approval/session/audit model.

**Architecture:** Extend the existing SSH target/profile/action model instead of introducing a second execution path. Mutating actions must stay server-side configured, separately profiled, tightly allowlisted, and explicitly visible in audit/results.

**Tech Stack:** C#/.NET 10, ASP.NET Core/FastEndpoints, Clean Architecture, EF Core with provider-neutral SQLite/PostgreSQL support, xUnit v3, ASP.NET Options, Docker Compose, SSH.NET-based connector.

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

### Slice W2a — `service.restart` compose-verified vertical slice

Goal: Smallest truthful end-to-end mutating action through the existing approval/session/action flow, with a real demo-side effect on the local Compose SSH target.

Binding W2a decisions:

- `service.restart` stays on the existing typed session-action endpoint; no new public API.
- Authorization uses a separate maintenance profile: `remote.maintenance.basic`.
- `remote.readonly.inspect` must not authorize `service.restart`.
- Parameters stay strictly allowlisted with exactly one string parameter: `service`.
- The development/demo target uses a dedicated demo service name such as `demo-app`, not `sshd`, so the running SSH connection is not destabilized by the restart demo.
- The demo implementation may realize the restart effect through the existing forced-command wrapper as long as the Gatekeeper side still models it as a normal typed SSH action.
- W2a must make mutating intent visible in both action result payloads and audit details with explicit `isMutating=true` and `risk=High` markers.
- W2a must prove a real demo-side effect, not only a policy- or fake-executor success path.
- W2a is intentionally limited to `service.restart` with exactly one allowlisted value: `service=demo-app`.

Expected tests:

- Approved maintenance profile can execute `service.restart` for the allowlisted demo service.
- Read-only profile cannot execute `service.restart`.
- Invalid/non-allowlisted service is denied.
- Audit/result clearly mark the action as mutating.
- The local Compose demo target exposes a bounded, observable restart effect for `demo-app`.

Acceptance criteria for W2a:

1. A session with target-scoped grant `("demo-ssh", "remote.maintenance.basic")` can execute `service.restart` with `service=demo-app`.
2. A session with target-scoped grant `("demo-ssh", "remote.readonly.inspect")` cannot execute `service.restart`.
3. `service.restart` rejects any non-allowlisted `service` value.
4. Successful action responses expose `result.isMutating=true` and `result.risk="High"`.
5. `SessionActionAllowed` and `SessionActionExecuted` audit details expose `IsMutating=true` and `Risk="High"`.
6. The Compose demo proves a real restart side effect via demo-owned state, without restarting `sshd` or requiring raw shell.

### Slice W2b — W2 truthfulness and operator docs

Goal: Make the first write-action slice easy to verify and hard to misunderstand for future agents and local operators.

Expected outputs:

- `docs/current-status.md` names W2a explicitly instead of the broader W2 label.
- `docs/phase-8-compose-ssh-demo.md` documents both read-only and maintenance smoke flows.
- `README.md` says `service.restart` is supported for the local Compose demo target only after W2a is actually validated.

### Slice W3a — `service.reload` vertical slice

Goal: Add a lower-impact sibling action with the same policy model as `service.restart`.

Binding W3a decisions:

- `service.reload` stays on the existing typed session-action endpoint; no new public API.
- Authorization continues to use `remote.maintenance.basic`.
- Parameters stay strictly allowlisted with exactly one string parameter: `service`.
- The local demo target uses the same bounded service name as restart: `service=demo-app`.
- W3a must expose mutating intent in both result payloads and audit listing with `isMutating=true` and `risk=High`.
- W3a must prove a real demo-side effect distinct from restart, so reload and restart stay operationally distinguishable.

Expected tests:

- Same authorization/allowlist rules as restart.
- Separate action name and audit trail.
- Compose demo exposes a bounded reload-specific state effect.

### Slice W3b — `backup.trigger` vertical slice

Goal: Add a non-service maintenance action that still fits the same typed model.

Binding W3b decisions:

- `backup.trigger` remains under the existing typed session-action endpoint.
- Authorization continues to use `remote.maintenance.basic`.
- Parameters stay strictly allowlisted with exactly one string parameter: `job`.
- The first Compose-demo scope stays intentionally tiny with one allowlisted job such as `nightly-config`.
- W3b must produce a bounded demo-owned side effect and bounded structured result/audit data.

Expected tests:

- Only allowlisted backup jobs may be triggered.
- Result/audit remain bounded and structured.
- Read-only profile still cannot execute the action.

### Slice W3c — Truthful support-surface updates

Goal: Keep the newly expanded maintenance surface short, demoable, and accurately documented.

Expected outputs:

- README short supported-actions section updated.
- `docs/current-status.md` canonical supported-actions section updated.
- `docs/phase-8-compose-ssh-demo.md` updated with reload/backup maintenance smokes.

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

## Planned Follow-up Architecture Phase

The current safe-write slices may continue on the existing config-backed action catalog, but further catalog breadth should not stay permanently config-first.

Follow-up plan:

- `docs/phase-12x-db-first-action-catalog.md`

Core follow-up decisions:

- DB-first action catalog as the long-term source of truth.
- Seed/import file allowed for bootstrap simplicity.
- Live-resolve against the current DB definition during action execution.
- Sessions continue to store grants, not full action snapshots.

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
