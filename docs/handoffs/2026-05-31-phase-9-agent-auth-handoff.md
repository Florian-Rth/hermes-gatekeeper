# Phase 9 Agent Auth Handoff

## Kurzstatus

Phase 9 ist **nicht fertig** und **nicht gepusht**.

Letzter sauberer Commit/Remote-Stand:

- `bcf1ea5` — `docs: plan phase 9 agent authentication`

Uncommitted Arbeitsstand liegt lokal auf `main`.

## Session-/Repo-Evidenz

- Relevante Session: `20260527_083413_fb993d`
- Letzter sichtbarer Session-Stand vor 429-Limit:
  - Todo: `a4. A4: Agent identity in Commands/Audit propagieren/reviewen/validieren (in_progress)`
  - danach sofort: `API call failed after 3 retries: HTTP 429: The usage limit has been reached`
- `git status --short --branch` zeigt lokale Änderungen + neue Dateien, aber keinen Commit nach `bcf1ea5`.

## Was lokal implementiert ist

### A1 — Agent Auth config/options
Vorhanden:
- `backend/src/Gatekeeper.Api/AgentAuthentication/AgentAuthOptions.cs`
- `backend/src/Gatekeeper.Api/AgentAuthentication/AgentApiKeyOptions.cs`
- `backend/src/Gatekeeper.Api/AgentAuthentication/AgentAuthConstants.cs`
- `backend/tests/Gatekeeper.Tests/AgentAuthOptionsTests.cs`

Abgedeckt:
- enabled/config binding
- duplicate/blank/whitespace validation
- fail-closed bei enabled ohne Keys
- kein Secret-Leak in `ToString()`/Fehlermeldungen

### A2 — API-key verifier + identity model
Vorhanden:
- `backend/src/Gatekeeper.Api/AgentAuthentication/AgentIdentity.cs`
- `backend/src/Gatekeeper.Api/AgentAuthentication/AgentAuthResult.cs`
- `backend/src/Gatekeeper.Api/AgentAuthentication/AgentApiKeyVerifier.cs`
- `backend/tests/Gatekeeper.Tests/AgentAuthApiKeyVerifierTests.cs`

Abgedeckt:
- success -> `agentId`, `authMethod=apiKey`
- `missing_key`, `malformed_key`, `invalid_key`, `auth_not_configured`
- fixed-time compare via SHA256 + `CryptographicOperations.FixedTimeEquals`
- kein Secret-Leak bei Failures

### A3 — geschützte Endpunkte
Vorhanden:
- `backend/src/Gatekeeper.Api/AgentAuthentication/AgentApiKeyGuard.cs`
- DI in `backend/src/Gatekeeper.Api/Program.cs`
- Schutz in:
  - `backend/src/Gatekeeper.Api/Endpoints/AccessRequests/CreateAccessRequestEndpoint.cs`
  - `backend/src/Gatekeeper.Api/Endpoints/Sessions/ExecuteSessionActionEndpoint.cs`
- Integrationstests erweitert in:
  - `backend/tests/Gatekeeper.Tests/AccessRequestEndpointTests.cs`
  - `backend/tests/Gatekeeper.Tests/AuditEventEndpointTests.cs`

Abgedeckt:
- 401 ohne Key
- 401 mit invalidem Key
- Admin-Cookie alleine autorisiert Agent-Endpunkte nicht
- Agent-Key autorisiert keine Admin-/Audit-Endpunkte
- Valid Requests behalten bisherigen Happy Path

### A4 — Agent Identity in Application/Audit
Vorhanden:
- `backend/src/Gatekeeper.Application/Common/AuthenticatedAgent.cs`
- `CreateAccessRequestCommand` erweitert um Agent
- `ExecuteSessionActionCommand` erweitert um Agent
- Endpoint-Mapping in beide Commands
- Audit-Anreicherung in:
  - `backend/src/Gatekeeper.Application/AccessRequests/AccessRequestService.cs`
  - `backend/src/Gatekeeper.Application/Sessions/SessionActionService.cs`

Aktueller Stand:
- `agentId`/`authMethod` werden in Success-/Denied-/Failed-Action-Auditpfade eingehängt
- SSH-Auditfelder bleiben laut aktueller Tests erhalten

## Was nach aktuellem Stand noch fehlt

### A5 — Failed agent auth audit
Nicht sichtbar implementiert:
- kein `AgentAuthenticationFailed`-Auditpfad gefunden
- aktuelle Unauthorized-Tests prüfen meist noch `0` Audit Events bzw. nur Statuscodes
- keine API-level Audit-Writer-Integration für Auth-Failures gefunden

### A6 — Demo config/docs/e2e smoke
Nicht sichtbar implementiert:
- `docs/current-status.md` noch alt: empfiehlt Phase 9 erst als nächsten Schritt
- README/Compose/Demo-Doku offenbar noch nicht auf Agent-Key-Fluss aktualisiert
- kein Commit/kein nachgewiesener Compose-Smoke-Run

## Validierung, die ich jetzt wirklich ausgeführt habe

Erfolgreich:

```bash
docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Gatekeeper.sln --filter "FullyQualifiedName~AgentAuthOptionsTests|FullyQualifiedName~AgentAuthApiKeyVerifierTests|FullyQualifiedName~AccessRequestEndpointTests|FullyQualifiedName~AuditEventEndpointTests"
```

Ergebnis:
- Passed: 84
- Failed: 0

## Aktuelle lokale Dateien mit Änderungen

Modified:
- `backend/src/Gatekeeper.Api/Endpoints/AccessRequests/CreateAccessRequestEndpoint.cs`
- `backend/src/Gatekeeper.Api/Endpoints/Sessions/ExecuteSessionActionEndpoint.cs`
- `backend/src/Gatekeeper.Api/Program.cs`
- `backend/src/Gatekeeper.Application/AccessRequests/AccessRequestService.cs`
- `backend/src/Gatekeeper.Application/AccessRequests/CreateAccessRequestCommand.cs`
- `backend/src/Gatekeeper.Application/Sessions/ExecuteSessionActionCommand.cs`
- `backend/src/Gatekeeper.Application/Sessions/SessionActionService.cs`
- `backend/tests/Gatekeeper.Tests/AccessRequestEndpointTests.cs`
- `backend/tests/Gatekeeper.Tests/AuditEventEndpointTests.cs`

Untracked:
- `backend/src/Gatekeeper.Api/AgentAuthentication/`
- `backend/src/Gatekeeper.Application/Common/AuthenticatedAgent.cs`
- `backend/tests/Gatekeeper.Tests/AgentAuthApiKeyVerifierTests.cs`
- `backend/tests/Gatekeeper.Tests/AgentAuthOptionsTests.cs`

## Nächste sinnvolle Schritte

1. A5 implementieren: `AgentAuthenticationFailed` audit event + safe payload (`method`, endpoint/route, reasonCode, authMethod=apiKey; keine Secrets/Headers/Bodies).
2. Tests anpassen/ergänzen, sodass fehlende/invalid Keys Audit-Events schreiben.
3. A6 umsetzen: Compose/demo config, README, `docs/current-status.md`, Phase-8-Doku/Curl-Beispiele mit `X-Gatekeeper-Agent-Key`.
4. Danach volle Validation aus Phase-9-Plan laufen lassen.
5. Erst dann committen und pushen.
