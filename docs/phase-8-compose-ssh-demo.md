# Phase 8 Compose SSH Demo

This demo is the canonical local end-to-end path for the generic SSH read-only connector.

It starts three controlled local services with Docker Compose:

- `backend`: Gatekeeper API, with the SSH connector configured for `demo-ssh`.
- `frontend`: the existing Gatekeeper web UI.
- `demo-ssh`: a local OpenSSH target container with the low-privilege user `gatekeeper-readonly`.

The files under `demo/ssh-client/` and `demo/ssh-target/` are deterministic demo-only material so the local Compose demo can run without external setup. They are not production credentials and must be replaced for any real deployment.

## Security boundaries demonstrated

The Compose demo keeps the Phase 8 boundaries intact:

- The agent requests `target` + capability profile approval, not credentials.
- The agent executes `target` + named `action` + typed `parameters`, not a raw shell command.
- Gatekeeper resolves approved profile membership and server-side command mappings.
- The demo SSH target runs as `gatekeeper-readonly`, with password login, root login, TTY allocation, SFTP, and forwarding disabled in the target SSH daemon. The demo authorized key is bound to a forced command wrapper that accepts only the same read-only commands Gatekeeper maps server-side.
- Configured actions are read-only: `system.status.read`, `disk.usage.read`, and `service.status.read` for the allowlisted service name `sshd`.
- Audits record safe metadata and outcome details, not SSH key content or unbounded command output.

## Start the demo

From the repository root:

```bash
docker compose config
docker compose build
docker compose up
```

Services:

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5209`
- Backend health: `http://localhost:5209/health`
- SSH target: internal Compose service name `demo-ssh`; it is not published on a host port.

Default local admin login for the Compose demo is controlled by the existing environment defaults in `compose.yml`: username `admin`, password `dev-admin-password-local-only`. Override them with `GATEKEEPER_ADMIN_USERNAME` and `GATEKEEPER_ADMIN_PASSWORD` for local experiments.

The Compose demo also enables Agent Auth with explicit demo-only values:

- `GATEKEEPER_AGENT_AUTH_ENABLED=true`
- `GATEKEEPER_AGENT_AUTH_DEMO_AGENT_ID=compose-demo-agent`
- `GATEKEEPER_AGENT_AUTH_DEMO_KEY=dev-compose-agent-key-local-only`

These values are intentionally local demo placeholders, not deployable secrets. For the shell examples below, export the demo key into a request variable:

```bash
export GATEKEEPER_AGENT_KEY="${GATEKEEPER_AGENT_KEY:-dev-compose-agent-key-local-only}"
```

## Browser flow

1. Open `http://localhost:5173`.
2. Log in as the local admin.
3. Create an access request through the API or another client with:
   - target: `demo-ssh`
   - requested capability/profile: `remote.readonly.inspect`
4. In the UI, select the pending request and approve it.
5. Execute an SSH action through the session action API:
   - target: `demo-ssh`
   - action: `system.status.read`, `disk.usage.read`, or `service.status.read`
6. Review the audit feed for request creation, approval/session creation, action allowed/executed, and SSH action metadata.

The current UI can display requests, sessions, action results, lifecycle state, and audit events. It does not include SSH target management UI; the SSH policy remains server-side configuration.

## Curl flow

The following commands exercise the full request -> approve -> execute -> audit flow through the backend API. They assume the Compose defaults and run from the repository root while `docker compose up` is running.

In these examples, `requester` is caller-supplied request metadata. The authenticated actor used for audit attribution comes independently from the Agent API key and appears in successful audit details as `agentId` plus `authMethod=apiKey`.

First, verify the protected agent endpoints fail closed without a key:

```bash
curl -i -sS http://localhost:5209/api/v1/access-requests \
  -H 'Content-Type: application/json' \
  -d '{"intent":"missing-key-smoke","requester":"compose-demo-agent","targets":["demo-ssh"],"requestedCapabilities":["remote.readonly.inspect"],"durationMinutes":15,"risk":"low","justification":"missing key smoke","proposedActions":["system.status.read"],"forbiddenActions":["raw shell"]}'
```

Create a request for the demo target/profile with the demo Agent key:

```bash
REQUEST_ID=$(curl -sS http://localhost:5209/api/v1/access-requests \
  -H 'Content-Type: application/json' \
  -H "X-Gatekeeper-Agent-Key: ${GATEKEEPER_AGENT_KEY}" \
  -d '{
    "intent":"Inspect the local demo SSH target",
    "requester":"compose-demo-agent",
    "targets":["demo-ssh"],
    "requestedCapabilities":["remote.readonly.inspect"],
    "durationMinutes":15,
    "risk":"low",
    "justification":"Local Compose read-only SSH connector demo",
    "proposedActions":["system.status.read","disk.usage.read","service.status.read"],
    "forbiddenActions":["raw shell","sudo","write actions","file transfer","port forwarding"]
  }' | jq -r '.id')
```

Log in as the local admin and keep the HttpOnly cookie in a curl cookie jar:

```bash
curl -sS -c /tmp/gatekeeper-demo-cookies.txt \
  http://localhost:5209/api/v1/admin/login \
  -H 'Content-Type: application/json' \
  -H 'Origin: http://localhost:5209' \
  -d '{"username":"admin","password":"dev-admin-password-local-only"}'
```

Approve the request and capture the session id:

```bash
SESSION_ID=$(curl -sS -b /tmp/gatekeeper-demo-cookies.txt \
  -X POST "http://localhost:5209/api/v1/access-requests/${REQUEST_ID}/approve" \
  -H 'Content-Type: application/json' \
  -H 'Origin: http://localhost:5209' \
  -d '{"comment":"Approve local read-only SSH demo"}' | jq -r '.sessionId')
```

Execute a configured read-only SSH action:

```bash
curl -i -sS "http://localhost:5209/api/v1/sessions/${SESSION_ID}/actions" \
  -H 'Content-Type: application/json' \
  -d '{"target":"demo-ssh","action":"system.status.read","parameters":{}}'
```

The previous call should return `401 Unauthorized` without the header. Retry with the demo Agent key:

```bash
curl -sS "http://localhost:5209/api/v1/sessions/${SESSION_ID}/actions" \
  -H 'Content-Type: application/json' \
  -H "X-Gatekeeper-Agent-Key: ${GATEKEEPER_AGENT_KEY}" \
  -d '{"target":"demo-ssh","action":"system.status.read","parameters":{}}' | jq
```

Execute the parameter-allowlisted service status action:

```bash
curl -sS "http://localhost:5209/api/v1/sessions/${SESSION_ID}/actions" \
  -H 'Content-Type: application/json' \
  -H "X-Gatekeeper-Agent-Key: ${GATEKEEPER_AGENT_KEY}" \
  -d '{"target":"demo-ssh","action":"service.status.read","parameters":{"service":"sshd"}}' | jq
```

List failed-auth audit events and verify the response does not expose the API key:

```bash
curl -sS -b /tmp/gatekeeper-demo-cookies.txt \
  "http://localhost:5209/api/v1/audit-events?eventType=AgentAuthenticationFailed&limit=50" | jq
```

Expected failed-auth details are bounded to fields such as route template, HTTP method, reason code, and `authMethod=apiKey`. The response must not contain the Agent key value.

List request-level audit events:

```bash
curl -sS -b /tmp/gatekeeper-demo-cookies.txt \
  "http://localhost:5209/api/v1/audit-events?aggregateId=${REQUEST_ID}&limit=50" | jq
```

Expected request-level audit event types include `AccessRequestCreated` and `AccessRequestApproved`. Successful `AccessRequestCreated` details should include `agentId=compose-demo-agent` and `authMethod=apiKey`.

List session/action audit events:

```bash
curl -sS -b /tmp/gatekeeper-demo-cookies.txt \
  "http://localhost:5209/api/v1/audit-events?aggregateId=${SESSION_ID}&limit=50" | jq
```

Expected session/action audit event types include `SessionCreated`, `SessionActionRequested`, `SessionActionAllowed`, and `SessionActionExecuted`. Successful action events should include `agentId=compose-demo-agent` and `authMethod=apiKey`. SSH execution audit details include target alias, action name, safe parameter summary where applicable, exit status, duration, timeout/truncation flags, output size metadata, and reason code.

## Optional real-VM configuration notes

To point Gatekeeper at a real SSH target instead of the local demo container:

1. Create a dedicated low-privilege account on the VM for Gatekeeper read-only inspection.
2. Install only the read-only tools needed by the configured actions.
3. Add a deployment-specific public key to that account's `authorized_keys`.
4. Store the matching private key outside the repository and mount it read-only into the backend container.
5. Pin the VM host key in a deployment-specific `known_hosts` file and mount it read-only into the backend container.
6. Configure a new `SshConnector:Targets:<alias>` entry with host, port, username, key path, known-hosts path, timeouts, output limits, profiles, actions, and parameter allowlists.
7. Keep the connector generic and read-only: no raw shell endpoint, sudo, write actions, TTY/interactivity, file transfer, or port forwarding.
8. Approve only the target alias and profile needed for the task, then execute named actions through `POST /api/v1/sessions/{sessionId}/actions`.

Do not reuse the Compose demo key material for any real VM or shared environment.
