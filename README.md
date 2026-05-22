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

Die Konzeptdokumente sind vorhanden. Eine erste technische Baseline existiert:

- `backend/`: .NET Solution mit ASP.NET-Core/FastEndpoints API und Health-Endpunkt unter `/health`
- `frontend/`: React/Vite-App mit pnpm-Skripten für Check, Test und Build
- Docker-Compose-Baseline für lokale Demo-/Dev-Starts mit Backend und statisch ausgeliefertem Frontend

Noch nicht implementiert sind Authentifizierung, Domänenlogik, Genehmigungsflows, Persistenz und Adapter.

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

Die Beispielwerte sind bewusst keine Secrets und nur für lokale Entwicklung gedacht.

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

## Dokumente

- `docs/vision.md` — Zielbild, Motivation, Kernprinzipien
- `docs/architecture.md` — Architekturentwurf, Komponenten, Datenflüsse
- `docs/interface-model.md` — HTTP API vs. Hermes Toolset, Agent Interface
- `docs/mvp-scope.md` — erster generischer MVP-Scope
- `docs/decisions.md` — getroffene Entscheidungen und offene Fragen
- `docs/research-existing-systems.md` — Recherche zu bestehenden ähnlichen Systemen

## Arbeitstitel

Hermes Gatekeeper
