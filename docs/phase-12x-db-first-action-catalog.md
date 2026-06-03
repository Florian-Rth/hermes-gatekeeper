# Phase 12.x Implementation Plan — DB-First Action Catalog

> **For Hermes:** Use `subagent-driven-development` to implement this plan task-by-task. Use fresh subagents per slice, with spec review first and code quality/security review second.

**Goal:** Replace the current config-first SSH action catalog with a DB-first catalog without redesigning Gatekeeper's approval/session/action flow.

**Architecture:** Keep the public action contract and the session grant model, but move targets, profiles, actions, parameter allowlists, and mutating/risk metadata into the database as the runtime source of truth. A seed file may bootstrap or explicitly reseed the catalog, but it must not remain a parallel runtime authority.

**Tech Stack:** C#/.NET 10, ASP.NET Core/FastEndpoints, Clean Architecture, EF Core with provider-neutral SQLite/PostgreSQL support, xUnit v3, Docker Compose.

---

## Binding Phase Decisions

### Decision 1 — Source of truth

Chosen: Full DB-first catalog. Config/seed files are bootstrap/import input, not a permanent equal runtime path.

Consequence:

- No long-term hybrid with two authoritative policy sources.
- Runtime resolve, approval validation, and later UI/API work can all target one model.
- The current ASP.NET config model becomes migration/seed input only.

### Decision 2 — Resolve behavior for running sessions

Chosen: Live-resolve against the current DB definition at action execution time.

Consequence:

- This intentionally follows Florian's latest choice: B.
- Sessions do not freeze full action definitions.
- Disabling or tightening an action in the DB affects still-open sessions immediately.
- We keep the system simple and operationally steerable instead of building action version snapshots now.

### Decision 3 — What the session stores

Chosen: Sessions continue to store target/profile grants, not concrete action snapshots.

Consequence:

- Minimal change to the current domain model.
- No large session migration beyond what is needed for catalog lookup semantics.
- The DB-first phase stays a catalog/resolve migration, not a session redesign.

### Decision 4 — Approval granularity

Chosen: Approval remains target + profile based.

Consequence:

- The admin/user-facing approval model stays understandable.
- We do not expand this phase into per-action approval UX.
- Profiles remain the human approval unit; actions remain catalog content.

### Decision 5 — Catalog modeling depth

Chosen: Persist targets, profiles, actions, profile-action membership, parameter allowlists, and mutating/risk metadata in normalized DB structures.

Consequence:

- "DB-first" is real, not just a JSON blob mirror.
- The catalog becomes queryable and future UI/API capable.
- Slightly more upfront modeling work is accepted to avoid another migration soon after.

### Decision 6 — Seed strategy

Chosen: Initial setup seeds the DB from a seed/config file. Later reseed/import is an explicit operator action, not an automatic startup overwrite.

Consequence:

- A large initial action set stays easy to ship.
- Operators do not lose DB changes on every restart.
- We avoid building a policy-editing UI in the same phase.

### Decision 7 — Stable runtime keys

Chosen: Public/runtime references continue to use stable names and aliases such as target alias, profile name, and action name.

Consequence:

- Existing request/approval/action APIs do not need an ID-based redesign.
- DB IDs remain internal persistence details.
- Docs, tests, and the demo flow stay readable.

### Decision 8 — Missing or disabled catalog entries

Chosen: Live-resolve stays fail-closed. Missing, disabled, or no-longer-matching catalog entries are denied.

Consequence:

- No special historical compatibility mode for open sessions.
- No hidden fallback back to config.
- Security is still sane, but we avoid heavy enterprise policy/versioning machinery.

## Scope

### In scope

- Add a DB catalog for typed action definitions.
- Seed that catalog from a file during setup / explicit import.
- Switch runtime action resolve from startup config objects to DB reads.
- Keep the existing public session action contract (`target` + `action` + typed `parameters`).
- Keep target/profile-based approval and session grants.
- Update docs so future agents know config-first is no longer the target architecture.

### Out of scope

- No admin UI for editing targets/profiles/actions.
- No public CRUD API for policy/catalog management yet.
- No action-version history system.
- No snapshotting full action definitions into sessions.
- No multi-admin approval, OIDC, mTLS, break-glass flow, or other broader hardening detours.
- No new connectors/adapters in the same phase.
- No raw shell or generic write primitive.

## Suggested Implementation Slices

### Slice D1 — DB catalog schema and seed import

Goal: Introduce the persistence model for targets, profiles, actions, allowlists, and action metadata.

Expected outcomes:

- EF entities + migration for the catalog.
- Seed/import format documented and validated.
- Initial development/demo catalog can be loaded into the DB.
- Existing runtime path may still use config temporarily during this slice.

Expected tests:

- Seed import creates the expected targets/profiles/actions.
- Invalid seed data fails clearly.
- Mutating/risk metadata persists correctly.
- Parameter allowlists persist correctly.

### Slice D2 — Runtime resolve reads from DB

Goal: Replace config-backed action resolution with DB-backed action resolution while preserving the public API contract.

Expected outcomes:

- `target` + `action` + `parameters` resolve against the DB catalog.
- Session grants still use target/profile semantics.
- Existing typed dummy vs SSH action endpoint shape remains unchanged.

Expected tests:

- Known target/action/profile combinations still succeed.
- Unknown/missing/disabled catalog entries fail closed.
- Parameter allowlists still enforce exact safe values.
- Mutating/risk metadata still reaches result/audit paths.

### Slice D3 — Approval/session consistency against the DB catalog

Goal: Make approval and session creation consistent with the DB-first catalog without redesigning approval UX.

Expected outcomes:

- Requested target/profile combinations are checked against the DB-backed catalog model where appropriate.
- Sessions still store grants, not snapshots.
- Current demo and maintenance flows continue to work after the catalog switch.

Expected tests:

- Approval cannot produce grants for nonexistent catalog targets/profiles.
- Running sessions deny actions whose catalog entries were removed/disabled.
- Existing demo maintenance/read-only flows still pass.

### Slice D4 — Operator/docs/handoff updates

Goal: Make the DB-first catalog operable and understandable for future sessions.

Expected outputs:

- `docs/current-status.md` reflects the truthful source of truth.
- `docs/implementation-plan.md` references this phase.
- `docs/decisions.md` records the binding architecture decisions.
- Setup/reseed operator flow is documented briefly and concretely.

## Validation Gates

Backend:

```bash
docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet restore Gatekeeper.sln && dotnet build Gatekeeper.sln --no-restore && dotnet test Gatekeeper.sln --no-build'

docker run --rm -u $(id -u):$(id -g) -e DOTNET_CLI_HOME=/tmp -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet tool restore --verbosity quiet >/dev/null && dotnet tool run csharpier -- check src tests'
```

Demo/operator sanity:

```bash
docker compose config
```

Truthfulness smoke after D2/D3:

```text
Seed/import catalog -> create request -> approve target/profile -> execute typed action -> verify DB-backed resolve path actually drove the result
```

## Commit Boundary

Phase 12.x is complete only when:

- The action catalog source of truth is the DB, not startup config.
- A seed/import path exists for the initial action set.
- Existing typed action flows still work through approval, session, execution, and audit.
- Docs truthfully describe the new architecture.
- Validation passes and the change is committed and pushed.
