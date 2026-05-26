# Phase 8 — Generic SSH Read-only Connector

## Status

Planned. This phase is now part of the MVP boundary. Do not replace it with a special-purpose connector.

## Phase Goal

Prove the actual Gatekeeper end goal in minimal real form: after an agent request and admin approval, Gatekeeper executes a bounded, audited, typed read-only action against a real target class through SSH.

The connector must stay generic. It must not become Home Assistant, Docker, Proxmox, or a free shell.

## MVP Decision

A Dummy adapter alone is not enough for MVP because it does not prove real target access. A generic SSH read-only connector belongs in the MVP because SSH is broadly applicable and can be constrained to typed, allowlisted, read-only commands.

## Non-Goals

This phase must not add:

- arbitrary command execution from agent-supplied command strings.
- free shell.
- sudo.
- write actions.
- TTY/interactivity.
- file upload/download.
- port forwarding.
- Home Assistant, Docker, Proxmox, Kubernetes, or other special-purpose adapters.
- UI for editing SSH targets or policies.

## Grill-Me Decisions

Phase-8 planning uses autonomous Grill-Me: the orchestrator answers questions from repository state, project docs, and prior decisions. Florian is only asked for genuinely non-inferable product, security, or architecture choices.

Resolved decisions for this phase:

- Approval grants a target plus a server-side capability profile, for example `demo-ssh` + `remote.readonly.inspect`.
- A capability profile contains server-side named actions. It is an approval/policy concept, not an agent-provided command bundle.
- Action execution uses `target` + `action` + `parameters`. The agent does not have to send the profile name for each execution.
- Gatekeeper checks whether any approved profile for the approved target contains the requested action.
- The canonical MVP/demo target is a controlled SSH container in Docker Compose. A real VM setup may be documented as an additional path, but it is not the required MVP validation path.
- Phase 8 reuses the existing session action endpoint and flow. Do not add a separate SSH-specific public API endpoint.
- SSH targets, profiles, actions, command mappings, parameter allowlists, timeouts, and output limits are configured server-side for the MVP. No DB or UI policy management in this phase.
- Frontend work is backend-first and minimal. Do not add SSH target management UI.

## Security Model

- Gatekeeper owns SSH credentials; agents never see them.
- SSH targets are named and configured server-side.
- SSH capability profiles are named and configured server-side.
- SSH actions are named and mapped server-side to concrete commands/templates.
- Agent execution request includes target, action, and typed parameters, not arbitrary shell text.
- Approved session must include the target and at least one profile that permits the requested action.
- Command timeout is mandatory.
- Output byte/line limit is mandatory.
- Stderr handling must be bounded.
- Non-zero exit is reported structurally when an allowed read-only command ran; it is not automatically a policy violation.
- Policy/adapter failures are distinct result categories, e.g. unknown target, unknown action, forbidden target/action/profile, invalid parameter, SSH connection/auth failure, timeout.
- Audit records action name, target alias, profile/capability context when available, safe parameter values, exit status, duration, output summary/truncation metadata, and reason codes; do not dump unrestricted raw output into audit details.

## Suggested Minimal Action Set

Start with low-risk read-only actions:

- `system.status.read`
  - Example command mapping: `uname -a && uptime` or safer separated command execution.
- `disk.usage.read`
  - Example command mapping: `df -h` with output limit.
- `service.status.read`
  - Parameter: service name from a server-side allowlist only.
- `ssh.command.read`
  - Internal/generic action type for configured named commands only; not arbitrary agent command input.

Exact command strings should be finalized during the phase Grill-Me and based on the test target environment.

## Configuration Shape

Prefer a config-file based MVP over DB/UI management. Use ASP.NET configuration binding from JSON/ENV-compatible options unless a later phase justifies YAML.

Conceptual shape:

```json
{
  "SshConnector": {
    "Targets": {
      "demo-ssh": {
        "Host": "demo-ssh",
        "Port": 22,
        "Username": "gatekeeper-readonly",
        "PrivateKeyPath": "/run/secrets/gatekeeper_demo_ssh_key",
        "KnownHostsPath": "/app/config/known_hosts",
        "DefaultTimeoutSeconds": 10,
        "DefaultOutputLimitBytes": 8192,
        "Profiles": {
          "remote.readonly.inspect": {
            "Actions": ["system.status.read", "disk.usage.read", "service.status.read"]
          }
        },
        "Actions": {
          "system.status.read": {
            "Command": ["uname", "-a"],
            "OutputLimitBytes": 8192
          },
          "disk.usage.read": {
            "Command": ["df", "-h"],
            "OutputLimitBytes": 8192
          },
          "service.status.read": {
            "CommandTemplate": ["systemctl", "is-active", "{service}"],
            "AllowedParameters": {
              "service": ["ssh"]
            },
            "OutputLimitBytes": 8192
          }
        }
      }
    }
  }
}
```

Commands must be modeled as argv arrays or strictly typed templates. Avoid shell-string concatenation. Template parameters must be allowlisted or strictly validated before execution.

## Backend Slices

### Slice B1 — Connector contracts and config model

Goal: Define SSH target/action/profile configuration and adapter contract without executing SSH yet.

Expected tests:

- Valid config loads named target, profile, and action.
- Approved profile membership can authorize a requested `target` + `action`.
- Missing target/action/profile returns a typed failure.
- Invalid action parameters return a typed failure.
- Agent-supplied arbitrary command is not part of the public action request contract.

### Slice B2 — SSH execution adapter

Goal: Add the infrastructure adapter that executes configured commands with timeout and output limits.

Expected tests:

- Successful read-only command returns bounded output.
- Timeout maps to typed adapter failure.
- Non-zero exit maps to typed adapter failure/result according to chosen contract.
- Output truncation is explicit.

### Slice B3 — Policy/action dispatch integration

Goal: Route approved SSH capabilities through the existing session action execution path.

Expected tests:

- Approved target + capability can execute mapped SSH action.
- Wrong target is denied.
- Wrong capability is denied.
- Unknown SSH target/action is denied/fails safely.
- Session budget and lifecycle rules still apply.

### Slice B4 — Audit integration

Goal: Audit SSH read-only action decisions and results without leaking secrets or unrestricted output.

Expected tests:

- Audit includes target alias, capability, exit status, duration, truncation flag.
- Audit does not include private key path content, command secrets, raw unrestricted output, or credentials.

### Slice B5 — Compose/demo test target

Goal: Provide the canonical safe local/demo way to validate the SSH connector.

Direction:

- Add a controlled test SSH container to Docker Compose.
- Use a read-only low-privilege user.
- Keep credentials local/demo-only and never document private secret values in prose.
- Document an optional real-VM setup path separately.

Expected validation:

- `docker compose up` can run Gatekeeper and the demo SSH target.
- Full flow works: request -> approve -> SSH read-only action -> audit.

## Frontend Scope

Frontend changes should be minimal. The current UI can already show request capabilities, action results, lifecycle, and audit events. Only add frontend work if needed for:

- choosing/running an SSH demo action from the UI.
- displaying SSH action result metadata clearly.
- documenting/labeling that actions are read-only and typed.

Do not add SSH target management UI in this phase.

## Validation Gates

Backend:

```bash
docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet restore Gatekeeper.sln && dotnet build Gatekeeper.sln --no-restore && dotnet test Gatekeeper.sln --no-build'

docker run --rm -u $(id -u):$(id -g) -e DOTNET_CLI_HOME=/tmp -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet tool restore --verbosity quiet >/dev/null && dotnet tool run csharpier -- check src tests'
```

Frontend if touched:

```bash
pnpm --dir frontend check
pnpm --dir frontend test -- --run
pnpm --dir frontend build
```

Compose:

```bash
docker compose config
docker compose build
```

## Commit Boundary

Phase 8 is complete only when:

- SSH read-only connector is implemented and tested.
- Full approved-session SSH action flow is validated.
- Audit behavior is reviewed.
- Docs explain configuration and demo setup.
- Spec and code-quality/security reviews approve.
- Changes are committed and pushed.
