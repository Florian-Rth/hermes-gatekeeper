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
- Frontend: React
- Deployment: Docker / Docker Compose
- Datenbank: SQLite für MVP, Postgres optional später
- Auth MVP: lokale Admin-Auth

## Dokumente

- `docs/vision.md` — Zielbild, Motivation, Kernprinzipien
- `docs/architecture.md` — Architekturentwurf, Komponenten, Datenflüsse
- `docs/interface-model.md` — HTTP API vs. Hermes Toolset, Agent Interface
- `docs/mvp-scope.md` — erster generischer MVP-Scope
- `docs/decisions.md` — getroffene Entscheidungen und offene Fragen
- `docs/research-existing-systems.md` — Recherche zu bestehenden ähnlichen Systemen

## Aktueller Status

Initiale Konzept- und Dokumentationsphase. Noch keine Implementierung.

## Arbeitstitel

Hermes Gatekeeper
