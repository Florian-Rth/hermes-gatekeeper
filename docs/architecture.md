# Hermes Gatekeeper — Architekturentwurf

## High-Level Architektur

```text
+------------------+       +--------------------------+       +---------------------+
| Hermes VM        |       | Gatekeeper VM            |       | Produktive Systeme  |
|                  |       |                          |       |                     |
| Hermes Agent     | ----> | Approval API             | ----> | Home Assistant      |
| Gatekeeper Tool  |       | Web UI                   | ----> | Andere VMs          |
|                  |       | Policy Engine            | ----> | Anderer Server      |
| Kein direkter    |       | Session Manager          | ----> | Docker / Proxmox    |
| Prod-Zugriff     |       | Action Proxy / Broker    |       |                     |
+------------------+       +--------------------------+       +---------------------+
```

## Komponenten

### 1. Hermes VM

Die bestehende VM, auf der Hermes läuft.

Standardzustand:

- kein direkter SSH-Zugriff auf produktive VMs
- keine Home-Assistant-Tokens
- keine Proxmox-/Docker-Admin-Tokens
- Zugriff auf Gatekeeper API
- lokale Arbeit weiterhin möglich

Hermes nutzt im MVP die HTTP API. Ein Hermes Toolset ist optionaler Adapter für später, nicht Kern des Produkts.

Siehe `docs/interface-model.md` für die genaue Abgrenzung zwischen HTTP API und Hermes Toolset.

### 2. Gatekeeper Service

Separat deploybarer Dienst, initial auf einer eigenen VM per Docker.

Das Produkt soll aber allgemein deploybar sein:

- Docker Compose für HomeLab/Self-Hosting
- später optional Kubernetes/Helm
- keine harte Kopplung an Florians Infrastruktur

Geplanter Tech Stack:

- Backend: neuestes .NET / ASP.NET Core
- API Framework: FastEndpoints
- API Dokumentation: OpenAPI/Swagger über FastEndpoints
- Frontend: React
- Datenbank: SQLite im MVP, Postgres optional später
- Auth MVP: lokale Admin-Auth

Aufgaben:

- REST API für Access Requests
- Web UI für Genehmigungen
- Authentifizierung für Florian
- Policy Engine
- Session Manager
- Audit Log
- Action Proxy / Broker zu Zielsystemen

### 3. Approval API

Nimmt strukturierte Zugriffsanfragen von Hermes an.

Beispiel:

```json
{
  "intent": "Investigate Home Assistant automation issue",
  "targets": ["home-assistant"],
  "capabilities": [
    "ha.read.states",
    "ha.read.logs",
    "ha.read.automations"
  ],
  "duration_minutes": 20,
  "risk": "low",
  "justification": "Debug why bathroom motion light automation did not fire.",
  "proposed_actions": [
    "Read recent Home Assistant logs",
    "Read state history for binary_sensor.bad_motion",
    "Read automation config for bathroom light"
  ],
  "forbidden_actions": [
    "Change automation",
    "Restart Home Assistant",
    "Toggle entities"
  ]
}
```

### 4. Web UI

Zeigt offene Requests und laufende Sessions.

MVP-Ansichten:

- offene Genehmigungsanfragen
- Request-Detailseite
- Approve / Deny
- laufende Sessions
- Revoke Session
- Audit Log

Die UI muss menschlich verständlich sein. JSON allein reicht nicht.

### 5. Policy Engine

Prüft Requests und Actions gegen Regeln:

- Ist das Zielsystem bekannt?
- Sind Capabilities auf diesem Ziel erlaubt?
- Ist die gewünschte Laufzeit unterhalb des Maximalwerts?
- Ist die Anfrage read-only oder write?
- Braucht sie stärkere Freigabe?
- Sind geplante Aktionen mit Scope kompatibel?

Policy soll perspektivisch als Code/Config versionierbar sein.

### 6. Session Manager

Erstellt bei Genehmigung eine Session.

Eine Session ist begrenzt durch:

- `expires_at`
- maximale Anzahl Aktionen
- Zielsysteme
- Capabilities
- optional: Services, Container, Pfade, Home-Assistant-Entitäten
- manueller Revoke
- Task-Completion

Empfohlen:

```text
Session endet bei: Ablaufzeit OR max actions erreicht OR manuell widerrufen OR Hermes meldet Aufgabe abgeschlossen.
```

### 7. Action Proxy / Broker

Führt genehmigte Aktionen gegen Zielsysteme aus.

Bevorzugt keine freie Shell, sondern typisierte Operationen:

- `get_service_status(host, service)`
- `read_service_logs(host, service, lines)`
- `list_docker_containers(host)`
- `read_docker_logs(host, container, lines)`
- `restart_docker_container(host, container)`
- `ha_get_state(entity_id)`
- `ha_get_logs(lines)`
- `ha_call_service(domain, service, target, data)`

Freie SSH-Befehle nur optional und stark begrenzt.

## API-Skizze

```text
POST /api/v1/access-requests
GET  /api/v1/access-requests
GET  /api/v1/access-requests/{id}
POST /api/v1/access-requests/{id}/approve
POST /api/v1/access-requests/{id}/deny

GET  /api/v1/sessions/{id}
POST /api/v1/sessions/{id}/actions
```

Noch geplant, aber nicht implementiert:

```text
GET  /api/v1/sessions
POST /api/v1/sessions/{id}/revoke
POST /api/v1/sessions/{id}/complete
GET  /api/v1/audit-events
```

`POST /api/v1/sessions/{sessionId}/actions` nimmt im aktuellen Dummy-MVP eine typisierte Capability und optionalen Payload:

```json
{
  "capability": "test.echo",
  "payload": {
    "message": "hello"
  }
}
```

## Capability-Level

### Level 0: Local only

Hermes bleibt auf der eigenen VM.

### Level 1: Read-only Observability

- Health Checks
- States lesen
- Logs lesen
- `systemctl status`
- `journalctl`
- `docker ps`
- `docker logs`
- `df`, `free`, `uptime`

### Level 2: Safe Maintenance

- einzelne erlaubte Services neu starten
- einzelne erlaubte Container neu starten
- Home Assistant Service Calls für freigegebene Domains/Entitäten
- Backups anstoßen

### Level 3: Change Session

- Konfiguration ändern
- Deployments
- Docker Compose Änderungen
- Home Assistant Automationen ändern

Erfordert Plan + explizite Freigabe.

### Level 4: Break-glass

- freie Shell / Root / Admin
- nur sehr kurz
- komplett auditiert
- nicht Standard

## Authentifizierung und Transport

Empfehlungen:

- Gatekeeper Web UI mit starker Authentifizierung schützen
  - bevorzugt OIDC über Authentik/Authelia/Keycloak, falls vorhanden
  - alternativ Login + TOTP
  - perspektivisch WebAuthn/Passkeys
- Hermes VM authentifiziert sich gegen Gatekeeper via mTLS oder festem Client Credential
- Gatekeeper hält Backend-Credentials zu Zielsystemen, nicht Hermes
- Session Tokens kurzlebig und widerrufbar

Aktueller Implementierungsstand:

- Web-Admin-Zugriff nutzt lokale Single-Admin-Auth mit HttpOnly Cookie-Session.
- Admin-Session-Endpunkte sind `POST /api/v1/admin/login`, `POST /api/v1/admin/logout` und `GET /api/v1/admin/me`.
- Approve, Deny, Revoke und Audit Listing sind durch die Admin-Session-Grenze geschützt; die UI verwendet keinen sichtbaren statischen Admin Token mehr.
- Session Action Execution ist im Dummy-MVP erreichbar, wird aber durch Session-ID, Expiry, Action-Budget und Capability-Allowlist begrenzt. Eine stärkere Agent-/Session-Authentifizierung ist später nachzuziehen.

## Audit Log

Pflichtdaten:

- Request ID
- Hermes Session / Caller ID
- Request Payload
- Policy-Ergebnis
- Genehmiger
- Approval/Deny Zeitpunkt
- Session ID
- jede Action mit Ziel, Parametern, Ergebnis, Exit Code, Dauer
- Output gekürzt oder gehasht
- Revoke/Expire/Complete Event

Perspektivisch:

- JSONL append-only
- Hash Chain gegen Manipulation
- Export nach Loki/ELK/Grafana
- tägliche Backups

## Erste Integrationen

Empfohlene Reihenfolge:

1. Dummy Adapter für risikofreien End-to-End Flow
2. Generischer SSH-read-only Connector als MVP-Realitätsnachweis
3. MVP Hardening / Release Candidate
4. HTTP read-only Adapter
5. Docker read-only über SSH
6. Safe write actions
7. Home Assistant read-only Adapter
8. Proxmox Adapter
9. Advanced / Break-glass Modus
