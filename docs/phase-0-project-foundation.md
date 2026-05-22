# Phase 0 — Projektgrundlage und Entwicklungsrahmen

## Ziel

Phase 0 erzeugt ein lauffähiges Full-Stack-Skeleton für Hermes Gatekeeper. Danach existieren Backend, Frontend, Docker-Compose-Grundlage, Validierungsbefehle und Projektkonventionen so, dass alle späteren Phasen in kleinen TDD-/Feature-Slices darauf aufbauen können.

## Entscheidungen aus Grill-Me

- Repository enthält Backend und Frontend.
- Backend liegt verbindlich unter `backend/`.
- Frontend liegt verbindlich unter `frontend/`.
- Backend-Stack: neuestes verfügbares .NET SDK, ASP.NET Core, FastEndpoints, OpenAPI/Swagger.
- Backend-Architektur: Clean Architecture mit `Gatekeeper.Core`, `Gatekeeper.Application`, `Gatekeeper.Infrastructure`, `Gatekeeper.Api`.
- Backend-Persistenz ab Phase 1: EF Core + SQLite + Migrations.
- MVP Auth später: ENV-seeded lokaler Admin und statischer Agent Client Token.
- Frontend-Stack: React + Vite + MUI + TanStack Query + Zod + Biome + Vitest.
- Frontend Package Manager: pnpm.
- Docker Compose lebt auf Repo-Root-Ebene.

## Nicht-Ziele

- Keine AccessRequest-/Session-/Action-Domainlogik.
- Keine echte Auth-Implementierung.
- Keine EF-Core-Migrations in Phase 0, außer Projektpakete/Struktur werden vorbereitet.
- Keine Admin-Approval-UI außer minimalem App-Shell/Platzhalter.
- Keine Dummy Actions.
- Keine produktiven Adapter.

## Implementierungsslices

### Slice 0.1 — Tooling-Prerequisites prüfen/herstellen

Ziel: Lokale Umgebung kann Backend und Frontend bauen.

Aktionen:

- Prüfen: `dotnet --version`.
- Prüfen: `node --version`.
- Prüfen: `pnpm --version`.
- Falls `dotnet` fehlt: .NET SDK lokal für den User installieren oder dokumentiert verfügbar machen.
- Falls `pnpm` fehlt: pnpm via Corepack aktivieren.

Validierung:

- `dotnet --version` gibt eine SDK-Version zurück.
- `node --version` gibt eine Version zurück.
- `pnpm --version` gibt eine Version zurück.

### Slice 0.2 — Backend Solution Skeleton

Ziel: `backend/` enthält eine baubare Clean-Architecture-.NET-Solution.

Erwartete Struktur:

```text
backend/
├── Directory.Build.props
├── Directory.Packages.props
├── Gatekeeper.sln
├── src/
│   ├── Gatekeeper.Api/
│   ├── Gatekeeper.Core/
│   ├── Gatekeeper.Application/
│   └── Gatekeeper.Infrastructure/
└── tests/
    └── Gatekeeper.Tests/
```

Aktionen:

- Solution anlegen.
- Classlib-Projekte für Core, Application, Infrastructure anlegen.
- Web API Projekt für Api anlegen.
- xUnit-v3-Testprojekt anlegen.
- Projekt-Referenzen gemäß Clean Architecture setzen:
  - Application → Core
  - Infrastructure → Core + Application
  - Api → Application + Infrastructure
  - Tests → alle relevanten Projekte
- Central Package Management mit `Directory.Packages.props` einrichten.
- `TreatWarningsAsErrors`, Nullable, ImplicitUsings und moderne C# Settings in `Directory.Build.props` aktivieren.
- Keine Primary Constructors verwenden.

Validierung:

- `dotnet restore backend/Gatekeeper.sln`
- `dotnet build backend/Gatekeeper.sln --no-restore`
- `dotnet test backend/Gatekeeper.sln --no-build`

### Slice 0.3 — Backend API Baseline

Ziel: Api startet mit FastEndpoints, Swagger/OpenAPI und Health Endpoint.

Aktionen:

- FastEndpoints und Swagger/OpenAPI einrichten.
- Health Endpoint als FastEndpoints Endpoint anlegen.
- `AddApplication()` in Application bereitstellen.
- `AddInfrastructure(IConfiguration)` in Infrastructure bereitstellen.
- Api nutzt diese DI-Extensions.
- Minimaler Test prüft Health-Verhalten über öffentliche HTTP/TestHost-Schnittstelle.

Validierung:

- `dotnet test backend/Gatekeeper.sln --filter Should_ReturnOk_When_HealthEndpointIsCalled`
- `dotnet build backend/Gatekeeper.sln`
- Optional manuell: Backend starten und `/swagger` sowie Health Endpoint prüfen.

### Slice 0.4 — Frontend Skeleton

Ziel: `frontend/` enthält eine baubare React/Vite-App mit den vereinbarten Qualitätswerkzeugen.

Aktionen:

- Vite React TypeScript Projekt unter `frontend/` anlegen.
- pnpm Lockfile erzeugen.
- MUI installieren.
- TanStack Query installieren.
- Zod installieren.
- Vitest + Testing Library einrichten.
- Biome einrichten.
- `@/` Path Alias konfigurieren.
- Grundstruktur anlegen:
  - `frontend/src/features/`
  - `frontend/src/components/`
  - `frontend/src/lib/`
  - `frontend/src/styles/`
  - `frontend/src/routes/`
- Minimale App-Shell mit MUI Theme und Platzhalterseite anlegen.

Validierung:

- `pnpm --dir frontend install --frozen-lockfile` nach initialem Lockfile, sonst `pnpm --dir frontend install`.
- `pnpm --dir frontend check` oder konkrete Biome-Check-Scripts.
- `pnpm --dir frontend test -- --run`
- `pnpm --dir frontend build`

### Slice 0.5 — Docker Compose Baseline

Ziel: Repo-Root enthält Docker Compose, das Backend und Frontend grundsätzlich starten kann.

Aktionen:

- `docker-compose.yml` oder `compose.yml` auf Repo-Root-Ebene anlegen.
- Backend Dockerfile anlegen.
- Frontend Dockerfile anlegen.
- `.env.example` mit nicht-geheimen Beispielwerten anlegen.
- README lokale Entwicklungsbefehle ergänzen.

Validierung:

- `docker compose config`
- Wenn Docker verfügbar ist: `docker compose build`

## Commit Boundary

Phase 0 endet mit einem Commit und Push, wenn:

- Backend-Solution baut.
- Backend-Tests laufen.
- Frontend baut.
- Frontend-Tests laufen.
- Biome/CSharpier-Formatierung ist angewendet.
- Docker Compose config ist gültig.
- README enthält lokale Setup-/Validierungsbefehle.

Commit-Vorschlag:

```text
feat: scaffold gatekeeper full-stack foundation
```

## Risiken und Agenten-Fallen

- Nicht `src/` oder `web/` auf Root-Ebene verwenden; nur `backend/` und `frontend/`.
- Keine npm/yarn Lockfiles erzeugen; pnpm ist verbindlich.
- Kein ESLint/Prettier im Frontend einführen; Biome ist verbindlich.
- Keine FluentAssertions im Backend-Testprojekt einführen.
- Keine Primary Constructors in C# verwenden.
- Keine Domain-/Auth-/Persistence-Logik in Phase 0 vorwegnehmen.
- Codex darf Scaffolding übernehmen, aber Ergebnis muss gegen diese Phase geprüft werden.
