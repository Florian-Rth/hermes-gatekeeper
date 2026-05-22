# Phase 1 — Access Request Domain, Persistenz und API

## Ziel

Phase 1 liefert die erste echte fachliche Backend-Vertikale für Hermes Gatekeeper: Access Requests können per HTTP API erstellt, gespeichert, gelesen und gelistet werden. Persistenz läuft über EF Core + SQLite mit Initial Migration. Beim Erstellen eines Requests wird ein Audit Event geschrieben.

## Grill-Me Ergebnis

Eskalationsbedarf: nein.

Die Phase-1-Entscheidungen lassen sich aus bestehenden Dokumenten und Projektkonventionen ableiten. Offene Fragen zu Auth, UI, Sessions, Actions, Policy UI, produktiven Adaptern, mTLS/OIDC und Audit-Hardening werden bewusst in spätere Phasen verschoben.

## Selbst entschiedene Optionen

| Thema | Verworfene Optionen | Entscheidung | Grund |
| --- | --- | --- | --- |
| Phase-1-Schnitt | kompletter MVP-Flow; nur Domain ohne Persistenz/API | Backend-only AccessRequest-Vertikale | Klein genug für TDD, aber fachlich nutzbar und real persistiert. |
| Frontend | Admin UI starten; UI-Mock | Kein Frontend in Phase 1 | API/Persistenz zuerst stabilisieren. |
| Auth | ENV Admin und Agent Token sofort; Dummy Auth | Keine Auth in Phase 1 | Auth ist eigene Sicherheitsphase; keine temporären Sicherheitsannahmen einbauen. |
| Adapter/Actions | Dummy Adapter sofort; Fake Adapter in Tests | Kein Adapter in Phase 1 | Actions brauchen Sessions/Policy und sind spätere Phase. |
| Persistenz | In-Memory Repository; JSON-Datei/LiteDB | EF Core + SQLite + Migration | In `decisions.md` und Phase 0 entschieden. |
| API-Pfad | `/api/requests`; unversionierte API | `/api/v1/access-requests` | Versionierte HTTP API ist MVP-Vertrag. |
| Endpunkte | nur POST; zusätzlich approve/deny | POST, GET by id, GET list | MVP AccessRequest Scope ohne Approval-Flow. |
| Approval/Sessions | sofort mitbauen; Status weglassen | Status vorbereiten, keine approve/deny Endpunkte | Request braucht `pending`; Mutation kommt später. |
| IDs | Integer IDs; prefixed IDs; ULID | `Guid` als öffentliche ID | Keine neue Dependency, keine DB-Interna nach außen. |
| Listenfelder/Metadata | normalisierte Tabellen; CSV | JSON-Textspalten via EF Converter | Einfacher MVP-Start ohne frühe Übernormalisierung. |
| Domain vs. EF | EF Entities als Domain; DTO-only | Core-Domain + Infrastructure-Mapping | Clean Architecture bleibt verbindlich. |
| Audit | kein Audit; JSONL/Hash Chain sofort | SQLite `AuditEvent`, zunächst `AccessRequestCreated` | Audit ist MVP-Kern, Hardening später. |
| Policy | volle Policy Engine; keine Prüfung | Basisvalidierung/Invarianten | Policy Engine kommt später, aber invalide Requests werden verhindert. |
| Risiko-Werte | Freitext; break-glass sofort | `Low`, `Medium`, `High` | MVP braucht validierbare Werte; Break-glass ist später. |
| Zeitquelle | `DateTime.UtcNow` direkt; DB Defaults | `IClock`/`SystemClock` | Deterministische Tests und klare Boundary. |
| Tests | nur Unit; nur Endpoint | Domain/Application + API/EF Integration | Vertikale Slice soll Verhalten und reale SQLite-Kompatibilität prüfen. |
| SQLite Tests | EF InMemory; nur temporäre Datei | SQLite In-Memory wo passend | Näher an Produktion als EF InMemory. |
| Migration | `EnsureCreated`; DbContext ohne Migration | Initial EF Migration | Migrations ab Phase 1 sind entschieden. |
| OpenAPI | manuelle OpenAPI-Datei | FastEndpoints DTOs/OpenAPI nutzen | Stack-Entscheidung aus Phase 0. |
| Commit Boundary | viele Mikrocommits; MVP-Sammelcommit | ein sauberer Phase-1-Commit | Phase bleibt atomar und reviewbar. |

## In Scope

- Backend only.
- Clean Architecture beibehalten:
  - Core: Domain-Modelle und Invarianten.
  - Application: Use Cases, Ports, Result-/Details-Modelle.
  - Infrastructure: EF Core, SQLite, Repository, Migrations.
  - Api: FastEndpoints-Endpunkte, Request-/Response-DTOs, Validatoren.
- AccessRequest-Domain:
  - `Id`
  - `Intent`
  - `Requester`
  - `Targets`
  - `RequestedCapabilities`
  - `DurationMinutes`
  - `Risk`
  - `Justification`
  - `ProposedActions`
  - `ForbiddenActions`
  - `Metadata`
  - `Status` default `Pending`
  - `CreatedAt`
  - `UpdatedAt`
- AuditEvent-Domain/Persistenz für `AccessRequestCreated`.
- EF Core `GatekeeperDbContext` mit SQLite.
- Initial Migration.
- API-Endpunkte:
  - `POST /api/v1/access-requests`
  - `GET /api/v1/access-requests/{id}`
  - `GET /api/v1/access-requests`
- Tests nach TDD in vertikalen Slices.

## Nicht-Ziele

- Keine Admin Auth.
- Kein statischer Agent Token.
- Kein Login.
- Kein Frontend.
- Keine Approval UI.
- Keine approve/deny Endpunkte.
- Keine Session-Erstellung.
- Keine Action-Ausführung.
- Kein Dummy Adapter.
- Keine produktiven Adapter.
- Keine Policy UI.
- Keine JSONL-Audit-Ausgabe.
- Keine Hash Chain.
- Kein OIDC, mTLS oder Passkeys.
- Keine automatische Approval-Policy.
- Keine Hermes Toolset-, MCP- oder CLI-Integration.

## Implementierungsslices

### Slice 1.1 — Domain-Modell und Tests

Ziel: AccessRequest und AuditEvent als Core-Domain sauber modellieren.

Aktionen:

- `AccessRequest` Aggregate/Entity anlegen.
- `AccessRequestStatus` enum: `Pending`, `Approved`, `Denied` für spätere Phasen vorbereiten.
- `RiskLevel` enum: `Low`, `Medium`, `High`.
- `AuditEvent` Entity anlegen.
- Basis-Invarianten:
  - `intent` nicht leer.
  - `requester` nicht leer.
  - mindestens ein `target`.
  - mindestens eine `requestedCapability`.
  - `durationMinutes > 0`.
  - `risk` gültig.
- `IClock`/`SystemClock` als testbare Zeitquelle vorbereiten.

Validierung:

- Test: gültiger Request wird `Pending`.
- Test: leere Pflichtfelder werden abgelehnt.
- Test: `CreatedAt`/`UpdatedAt` werden gesetzt.
- Test: `AuditEvent` für `AccessRequestCreated` kann erzeugt werden.

### Slice 1.2 — Application Use Cases

Ziel: Access Requests ohne HTTP und ohne EF über Application-Ports erstellen und lesen.

Aktionen:

- `CreateAccessRequestCommand`.
- `AccessRequestDetails` und `AccessRequestSummary`.
- `IAccessRequestRepository` Port.
- `IAuditEventRepository` oder gemeinsame Persistence Boundary.
- `IAccessRequestService` / `AccessRequestService`.
- Use Cases:
  - Create.
  - Get by id.
  - List.

Validierung:

- Test: Create speichert pending AccessRequest.
- Test: Create schreibt `AccessRequestCreated` AuditEvent.
- Test: Get by id liefert gespeicherten Request.
- Test: List liefert Requests sortiert `createdAt` absteigend.

### Slice 1.3 — EF Core + SQLite + Migration

Ziel: Persistenz real machen.

Aktionen:

- EF Core Packages über Central Package Management ergänzen.
- `GatekeeperDbContext` in Infrastructure.
- EF Mappings.
- JSON-Persistenz für Listen und Metadata in SQLite-Textspalten.
- Repositories implementieren.
- SQLite Connection String über Configuration.
- Initial Migration anlegen.

Validierung:

- Test: Migration kann angewendet werden.
- Test: Request persistiert und wird korrekt geladen.
- Test: Listenfelder und Metadata roundtrip funktionieren.
- Test: AuditEvent persistiert.

### Slice 1.4 — API Endpoints

Ziel: HTTP API für Access Requests.

Aktionen:

- `POST /api/v1/access-requests`.
- `GET /api/v1/access-requests/{id}`.
- `GET /api/v1/access-requests`.
- Endpoint-lokale Request-/Response-DTOs und Validatoren.
- Manuelles Mapping Request → Command → Details/Summary → Response.
- Statuscodes:
  - Create success: `201 Created`.
  - Validation error: `400 Bad Request`.
  - Not found: `404 Not Found`.

Validierung:

- Test: POST erzeugt Request und gibt `201` zurück.
- Test: GET by id liefert Request.
- Test: unbekannte ID gibt `404`.
- Test: GET list enthält erzeugte Requests.
- Test: invalid POST gibt `400`.
- Test: nach POST existiert ein `AccessRequestCreated` AuditEvent.

### Slice 1.5 — Validation Gate und Cleanup

Ziel: Phase 1 sauber abschließen.

Validierung:

```bash
dotnet restore backend/Gatekeeper.sln
dotnet build backend/Gatekeeper.sln --no-restore
dotnet test backend/Gatekeeper.sln --no-build
cd backend && dotnet csharpier format . && dotnet csharpier check .
docker compose config
```

Wenn Docker verfügbar und Änderungen Docker betreffen:

```bash
docker compose build
```

## Commit Boundary

Phase 1 endet mit einem Commit und Push, wenn:

- alle Backend-Tests grün sind.
- Solution ohne Warnungen baut.
- CSharpier angewendet/geprüft wurde.
- Initial Migration enthalten ist.
- API-Endpunkte über FastEndpoints/OpenAPI erzeugbar sind.
- kein Frontend-, Auth-, Session-, Action- oder Adapter-Scope-Creep enthalten ist.

Commit-Vorschlag:

```text
feat: add access request persistence and api
```

## Agenten-Fallen

- Nicht den ganzen MVP bauen.
- Keine Auth in diese Phase ziehen.
- Keine UI starten.
- Kein EF InMemory Provider für SQLite-Verhalten verwenden.
- Keine Endpoint DTOs in Application Services leaken.
- Keine EF Entities über Service Boundaries leaken.
- Keine FluentAssertions.
- Keine Primary Constructors.
- Keine XML-Kommentare.
- Keine normalisierten Tabellen für Listenfelder erzwingen, solange kein Query-Bedarf besteht.
