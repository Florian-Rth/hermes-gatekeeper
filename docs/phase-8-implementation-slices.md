# Phase 8 Implementation Slices — Generic SSH Read-only Connector

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Implement the first real Gatekeeper MVP connector: a generic SSH read-only adapter that executes typed, allowlisted actions against a controlled target after approval.

**Architecture:** Approval grants a target plus a capability profile. Execution requests name a target, concrete action, and typed parameters. Gatekeeper resolves the approved profile/action relationship server-side, dispatches through the existing session action flow, and later uses an SSH infrastructure adapter for execution.

**Tech Stack:** C#/.NET, FastEndpoints, Clean Architecture, xUnit v3, ASP.NET Options, Docker Compose for demo validation.

---

## Binding Phase Decisions

- Approval: `target` + capability profile, e.g. `demo-ssh` + `remote.readonly.inspect`.
- Execution request: `target` + `action` + `parameters`.
- Agent never sends raw shell commands and never receives SSH credentials.
- SSH configuration is server-side config for the MVP; no DB/UI policy management.
- Canonical demo target is a controlled SSH container in Docker Compose.
- Keep using the existing session action endpoint; do not add SSH-specific public endpoints.
- Backend-first. Frontend only if existing action result display is insufficient.

## Task B1 — Connector contracts and configuration policy

**Objective:** Add the configuration/domain contract needed to authorize `target` + `action` from approved profiles without executing SSH.

**Scope:** Backend only. No SSH library. No Docker Compose changes. No endpoint contract change beyond types required by the existing action service.

**Files to inspect first:**

- `backend/src/Gatekeeper.Application/Sessions/SessionActionService.cs`
- `backend/src/Gatekeeper.Application/Sessions/ISessionActionAdapter.cs`
- `backend/src/Gatekeeper.Application/Sessions/ExecuteSessionActionCommand.cs`
- `backend/src/Gatekeeper.Infrastructure/SessionActions/DummySessionActionAdapter.cs`
- existing backend tests under `backend/tests`

**Expected files to add/modify:**

- Create application contracts for target/action authorization if they belong near session actions.
- Create infrastructure configuration model under `Gatekeeper.Infrastructure/SessionActions` or a dedicated SSH connector folder.
- Register options/services in `Gatekeeper.Infrastructure/DependencyInjection.cs` if needed.
- Add tests for config/profile/action resolution.

**Behavior tests:**

1. Valid target/profile/action config authorizes an action when the session has the matching target and profile.
2. Unknown target fails with a typed failure.
3. Unknown action fails with a typed failure.
4. Missing profile membership fails with a typed failure.
5. Invalid/unsupported parameter fails with a typed failure.
6. There is no public command string in the execution contract.

**TDD rule:** Implement one behavior at a time. Write one failing test, run it to confirm the expected failure, implement minimal code, then run the test again.

**Validation commands:**

```bash
docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Gatekeeper.sln

docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet restore Gatekeeper.sln >/dev/null && dotnet build Gatekeeper.sln --no-restore'

docker run --rm -u $(id -u):$(id -g) -e DOTNET_CLI_HOME=/tmp -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet tool restore --verbosity quiet >/dev/null && dotnet tool run csharpier -- check src tests'
```

**Commit boundary:** Commit after B1 passes spec review, quality/security review, and validation.

## Task B2 — SSH execution adapter

**Objective:** Implement the infrastructure adapter that executes configured read-only commands over SSH with timeout and bounded output.

**Depends on:** B1.

**Required behaviors:**

- Successful configured command returns bounded stdout/stderr and exit code.
- Timeout returns a typed timeout failure.
- Connection/auth failure returns a typed adapter failure.
- Output truncation is explicit.
- Non-zero exit is returned structurally when the allowed command ran.

## Task B3 — Session action integration

**Objective:** Route approved SSH actions through the existing session action execution path.

**Depends on:** B1, B2.

**Required behaviors:**

- Approved target + profile can execute an included action.
- Wrong target is denied.
- Action not included in approved profile is denied.
- Session lifecycle and action budget rules still apply.
- Execute endpoint accepts `target`, `action`, and `parameters`.

## Task B4 — Audit integration

**Objective:** Audit SSH action decisions/results without leaking secrets or unrestricted output.

**Depends on:** B3.

**Required behaviors:**

- Audit includes target alias, action, safe parameter summary, exit status, duration, timeout/truncation flags, output size metadata, and reason code.
- Audit does not include credentials, private key content, or unbounded raw output.

## Task B5 — Compose demo target and docs

**Objective:** Provide a reproducible local E2E demo for the SSH connector.

**Depends on:** B2-B4.

**Required behaviors:**

- Docker Compose can start backend, frontend, and controlled demo SSH target.
- Demo target uses a low-privilege user.
- Demo credentials are local/demo-only and are not documented as secrets in prose.
- Docs explain the full request -> approve -> execute -> audit flow.
- Docs include optional real-VM configuration notes.

## Final Phase Gate

Run:

```bash
git diff --check

docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet restore Gatekeeper.sln && dotnet build Gatekeeper.sln --no-restore && dotnet test Gatekeeper.sln --no-build'

docker run --rm -u $(id -u):$(id -g) -e DOTNET_CLI_HOME=/tmp -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet tool restore --verbosity quiet >/dev/null && dotnet tool run csharpier -- check src tests'

docker compose config
docker compose build
```

If frontend changes occur, also run:

```bash
pnpm --dir frontend check
pnpm --dir frontend test -- --run
pnpm --dir frontend build
```
