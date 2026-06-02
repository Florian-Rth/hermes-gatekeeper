# Hermes Gatekeeper

Hermes Gatekeeper ist ein geplanter Approval- und Capability-Broker für kontrollierte Hermes-Agent-Zugriffe auf produktive HomeLab-Systeme.

## Kurzfassung

Hermes soll grundsätzlich mächtig bleiben und langfristig viele Aufgaben im HomeLab übernehmen können. Gleichzeitig dürfen produktive Systeme außerhalb der Hermes-VM nicht dauerhaft oder ungeprüft erreichbar sein.

Hermes Gatekeeper soll deshalb als externer Genehmigungsdienst auf einer separaten VM laufen. Hermes fragt dort Zugriff an, Florian genehmigt oder lehnt die Anfrage in einer Web-View ab. Bei Genehmigung entsteht eine begrenzte Zugriffssession, über die Hermes ausschließlich die freigegebenen Capabilities nutzen kann.

## Produktziel

Hermes Gatekeeper soll nicht nur für Florians HomeLab funktionieren, sondern als allgemeines Open-Source-Projekt entworfen werden, das andere selbst deployen können.

Der Kern soll generisch bleiben:

- nicht Home-Assistant-first
- nicht Hermes-only
- nicht auf ein bestimmtes HomeLab zugeschnitten
- HTTP API first
- adapterbasiert für unterschiedliche Zielsysteme

## Geplanter Tech Stack

- Backend: neuestes .NET / ASP.NET Core
- API Framework: FastEndpoints
- Frontend: React / Vite
- Deployment: Docker / Docker Compose
- Datenbank: SQLite für MVP, Postgres optional später
- Auth MVP: lokale Admin-Auth

## Aktueller Status

Der MVP-Kern ist bis einschließlich des ersten kontrollierten Safe-Write-Slices implementiert. Die reproduzierbare lokale Compose-Demo mit generischem SSH-Connector funktioniert mit Agent Auth, Admin Cookie Login, Audit Attribution, read-only Actions und dem ersten allowlisteten Maintenance-Flow `service.restart`.

Aktuell funktioniert:

```text
Agent Authenticated Access Request -> Admin Login -> Approve/Deny in Web UI -> Session -> Execute typed dummy or SSH read-only action -> Lifecycle controls -> Audit browsing
```

Kurz dokumentierte aktuell unterstützte Actions:

- Dummy:
  - `test.echo`
  - `test.status.read`
  - `test.fail`
- SSH read-only im Compose-/Dev-Setup (`demo-ssh`, Profil `remote.readonly.inspect`):
  - `system.status.read`
  - `disk.usage.read`
  - `service.status.read` mit allowlistetem Parameter `service=sshd`

Als nächste konkrete Produktphase folgt jetzt die Erweiterung des Safe-Write-Sets um kleine Schwester-Actions wie `service.reload`, nicht weiteres allgemeines Hardening. Der Detailplan liegt in `docs/phase-12-safe-write-actions.md`.

Implementiert sind:

- `backend/`: .NET-10-Solution mit ASP.NET Core/FastEndpoints, EF Core, SQLite und Migrations
- `frontend/`: React/Vite-App mit Admin-Login, Approval-Dashboard, Session Lifecycle Controls, Audit Feed und pnpm-Skripten für Check, Test und Build
- Docker-Compose-Baseline für lokale Demo-/Dev-Starts inklusive kontrolliertem `demo-ssh` Target und demo-only Agent-Auth-Konfiguration
- Access-Request-API:
  - `POST /api/v1/access-requests`
  - `GET /api/v1/access-requests/{id}`
  - `GET /api/v1/access-requests`
- Admin-Auth-API mit lokaler Cookie-Session:
  - `POST /api/v1/admin/login`
  - `POST /api/v1/admin/logout`
  - `GET /api/v1/admin/me`
- Approval-/Deny-API über Admin-Session:
  - `POST /api/v1/access-requests/{id}/approve`
  - `POST /api/v1/access-requests/{id}/deny`
- Session-API:
  - `GET /api/v1/sessions/{id}`
  - `POST /api/v1/sessions/{sessionId}/actions`
  - `POST /api/v1/sessions/{id}/complete`
  - `POST /api/v1/sessions/{id}/revoke`
- Agent API Authentication für:
  - `POST /api/v1/access-requests`
  - `POST /api/v1/sessions/{sessionId}/actions`
  - Header: `X-Gatekeeper-Agent-Key`
- Dummy Action Adapter mit `test.echo`, `test.status.read` und `test.fail`
- Generischer SSH-Connector mit serverseitig konfiguriertem Demo-Target `demo-ssh`, Read-only-Profil `remote.readonly.inspect`, Maintenance-Profil `remote.maintenance.basic` und Actions `system.status.read`, `disk.usage.read`, `service.status.read`, `service.restart`
- Audit API und Events für Request-Erstellung, Admin Login/Logout, Approval/Deny, Session-Erzeugung, Lifecycle-Übergänge und Action-Entscheidungen/Ausführung
- Audit-Anreicherung für Agent Requests/Actions und bounded `AgentAuthenticationFailed` Events ohne API-Key-Leakage
- Approval-Web-UI:
  - lokalen Admin Login via HttpOnly Cookie-Session
  - Requests listen und Details menschenlesbar anzeigen
  - Approve/Deny mit Kommentar
  - Session Summary, Action Budget, Revoke/Complete und optionale Dummy Action anzeigen
  - Audit Events mit Filtern browsen

Noch nicht implementiert sind globale Session-Operations-UI, OIDC/TOTP/Passkeys/mTLS und Multi-Admin Approval. Als erster kontrollierter Safe-Write-Slice ist `service.restart` jetzt für den lokalen Compose-Demo-Target `demo-ssh` unterstützt, separat über `remote.maintenance.basic` profiliert und auf `service=demo-app` allowlistet. Weitere Write-Actions wie `service.reload`, `backup.trigger` oder `container.restart` bleiben noch aus. Spezielle Connectoren wie Home Assistant, Docker und Proxmox bleiben Post-MVP.

Der detaillierte Projektstand für zukünftige Agents steht in `docs/current-status.md`.

## Voraussetzungen

Für lokale Entwicklung ohne Container:

- .NET SDK 10 oder kompatibel zum Target Framework `net10.0`
- pnpm 11.x

Für Container-Starts:

- Docker
- Docker Compose v2 (`docker compose`)

## Lokale Entwicklung

### Backend

```bash
cd backend
dotnet restore Gatekeeper.sln
dotnet build Gatekeeper.sln
dotnet test Gatekeeper.sln
```

Optional lokal starten:

```bash
cd backend
dotnet run --project src/Gatekeeper.Api/Gatekeeper.Api.csproj
```

Der Development-Launch-Port ist `http://localhost:5209`; der Health-Endpunkt ist `http://localhost:5209/health`.

### Frontend

```bash
cd frontend
pnpm install
pnpm check
pnpm test
pnpm build
```

Optional Vite lokal starten:

```bash
cd frontend
pnpm dev
```

## Docker Compose

Beispielwerte stehen in `.env.example`. Für lokale Anpassungen kann die Datei kopiert werden:

```bash
cp .env.example .env
```

Die Beispielwerte sind bewusst keine Secrets und nur für lokale Entwicklung gedacht. Für lokale HTTP-Compose-Demos setzt die Beispielkonfiguration `GATEKEEPER_ADMIN_COOKIE_SECURE=false`; produktionsähnliche Deployments sollen Secure-Cookies verwenden. Dasselbe gilt für die Demo-Agent-Auth-Werte `GATEKEEPER_AGENT_AUTH_DEMO_AGENT_ID` und `GATEKEEPER_AGENT_AUTH_DEMO_KEY`.

Compose validieren, Images bauen und Services starten:

```bash
docker compose config
docker compose build
docker compose up
```

Ports der Compose-Baseline:

- Backend API: `http://localhost:5209`
- Backend Health: `http://localhost:5209/health`
- Frontend: `http://localhost:5173`
- Demo SSH target: interner Compose-Service `demo-ssh` auf Port 22, nicht auf den Host veröffentlicht

Die Compose-Demo konfiguriert zusätzlich eine lokale Demo-Agent-Auth für `X-Gatekeeper-Agent-Key` und den Backend-Connector für `demo-ssh` mit den lokalen Profilen `remote.readonly.inspect` und `remote.maintenance.basic`. Der vollständige Request -> Approve -> Execute -> Audit Ablauf für Read-only und den ersten Safe-Write-Slice steht in `docs/phase-8-compose-ssh-demo.md`.

## Dokumente

- `docs/current-status.md` — aktueller Projektstand, implementierte Phasen, bekannte Lücken und nächste sinnvolle Schritte
- `docs/phase-12-safe-write-actions.md` — Detailplan für die ersten kontrollierten Safe-Write-Actions
- `docs/phase-9-mvp-hardening-agent-auth.md` — abgeschlossener Detailplan für MVP-Härtung und Agent API Authentication
- `docs/phase-8-compose-ssh-demo.md` — lokale Compose-Demo für den generischen SSH-Connector mit Read-only- und erstem Maintenance-Flow
- `docs/vision.md` — Zielbild, Motivation, Kernprinzipien
- `docs/architecture.md` — Architekturentwurf, Komponenten, Datenflüsse
- `docs/interface-model.md` — HTTP API vs. Hermes Toolset, Agent Interface
- `docs/mvp-scope.md` — erster generischer MVP-Scope
- `docs/implementation-plan.md` — phasenorientierter Plan; ältere Phasennummern wurden inzwischen teilweise re-geschnitten
- `docs/decisions.md` — getroffene Entscheidungen und offene Fragen
- `docs/research-existing-systems.md` — Recherche zu bestehenden ähnlichen Systemen

## Arbeitstitel

Hermes Gatekeeper
