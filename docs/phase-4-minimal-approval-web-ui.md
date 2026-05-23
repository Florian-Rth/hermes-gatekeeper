# Phase 4 — Minimal Approval Web UI

> **For Hermes:** Use `florian-ai-coding-workflow`, `florian-frontend-work`, `florian-ux-expert`, and `subagent-driven-development`. Keep the phase small and shippable.

## Ziel

Phase 4 macht den Human-in-the-loop-Teil erstmals im Browser benutzbar. Die UI soll pending Access Requests menschenlesbar anzeigen und Approve/Deny gegen die bestehende Backend-API ausführen.

Der Backend-Kern ist bereits umgesetzt:

```text
Access Request -> Approve/Deny -> Session -> Execute typed dummy action -> Audit
```

Phase 4 baut darauf auf. Sie ersetzt keine Backend-Logik und führt keine produktiven Adapter ein.

## Entscheidung

Wir bauen eine minimale Admin-Web-UI ohne vollständigen Login. Der aktuelle Backend-Approval-Mechanismus nutzt `X-Gatekeeper-Admin-Token`. Deshalb bekommt die UI für diese Phase ein lokales Token-Eingabefeld, das nur im Browser-State gehalten wird.

Begründung:

- Der nächste Produktwert ist ein sichtbarer Approval-Flow.
- Vollständige Admin-Login-/Cookie-Auth bleibt eine eigene spätere Phase.
- Die UI kann jetzt schon gegen den vorhandenen, validierten Backend-Vertrag arbeiten.

## In Scope

### Frontend

- Ersetze die Skeleton-Startseite durch ein Admin-Dashboard.
- API Client mit Zod-validierten Response-Schemas.
- TanStack Query Hooks für:
  - Access Requests listen.
  - Access Request Details lesen.
  - Approve Request.
  - Deny Request.
  - Session Details lesen, wenn nach Approval eine Session entsteht.
  - Optional: Dummy Action ausführen, um den End-to-End-Kern in der UI sichtbar zu machen.
- Admin Token Eingabe:
  - lokaler UI-State.
  - kein Persistieren in LocalStorage in dieser Phase.
  - Token wird nur für Approve/Deny Requests als Header verwendet.
- Pending Request Liste.
- Request Detail Panel mit menschenlesbaren Feldern:
  - intent
  - requester
  - targets
  - requested capabilities
  - duration
  - risk
  - justification
  - proposed actions
  - forbidden actions
  - metadata
- Approve/Deny Actions mit Kommentar-Feld.
- Nach Approval Session Summary anzeigen.
- Loading, empty und error states.
- Tests für Hauptverhalten.

### Frontend Runtime / Dev Proxy

- Frontend API-Aufrufe sollen relative Pfade verwenden (`/api/v1/...`).
- Vite Dev Server soll `/api` und `/health` zum Backend `http://localhost:5209` proxien.
- Nginx Runtime soll `/api` und `/health` zum Compose-Service `backend:8080` proxien.

## Nicht-Ziele

- Keine echte Admin-Login-/Cookie-Session.
- Kein OIDC, Passkeys, TOTP oder mTLS.
- Keine produktiven Zielsystemadapter.
- Keine Policy UI.
- Keine Audit-Log-Ansicht, solange kein Audit API existiert.
- Kein Session revoke/complete, solange die Backend-Endpunkte nicht existieren.
- Kein Design-Finish. Gute Grund-UX, aber kein finaler Look.

## UX-Leitlinien

- Primary task: Admin entscheidet schnell und sicher, ob ein Request genehmigt werden darf.
- Liste links/oben, Detail rechts/unten je nach Bildschirmbreite.
- Kein rohes JSON als Hauptdarstellung.
- Risk, Duration, Targets und Capabilities müssen schnell erfassbar sein.
- Approve und Deny brauchen outcome-orientierte Labels:
  - “Approve request”
  - “Deny request”
- Deny darf nicht visuell schwächer sein als Approve; beide sind fachlich wichtige Entscheidungen.
- Fehlerzustände müssen sagen, was der Admin tun kann, z.B. “Admin token prüfen”.

## Vorgeschlagene Struktur

```text
frontend/src/features/access-requests/
├── index.ts
├── api.ts
├── schemas.ts
├── types.ts
├── components/
│   ├── AccessRequestDashboard/
│   │   ├── index.tsx
│   │   └── AccessRequestDashboard.test.tsx
│   ├── AccessRequestList/
│   │   └── index.tsx
│   ├── AccessRequestDetails/
│   │   └── index.tsx
│   ├── AdminTokenPanel/
│   │   └── index.tsx
│   └── RequestDecisionPanel/
│       └── index.tsx
```

Shared API helper falls nötig:

```text
frontend/src/lib/apiClient.ts
```

## API-Verträge

### List Access Requests

`GET /api/v1/access-requests`

Erwartet: Response mit `items`.

### Get Access Request

`GET /api/v1/access-requests/{id}`

### Approve

`POST /api/v1/access-requests/{id}/approve`

Header:

```http
X-Gatekeeper-Admin-Token: <token>
```

Body:

```json
{
  "comment": "Looks safe."
}
```

### Deny

`POST /api/v1/access-requests/{id}/deny`

Header analog Approve.

### Get Session

`GET /api/v1/sessions/{id}`

### Optional Dummy Action

`POST /api/v1/sessions/{sessionId}/actions`

Nur für sichtbaren Demo-Loop, nicht als Hauptfeature der Approval UI.

## Implementierungsslices

### Slice 4.1 — API Boundary und Runtime Proxy

- `src/lib/apiClient.ts` oder feature-lokaler Fetch Helper.
- Zod Schemas für Access Request Summary/Details, ApprovalResult, DenialResult, SessionDetails.
- TanStack Query Hooks im Feature `api.ts`.
- Vite Proxy für `/api` und `/health`.
- Nginx Proxy für `/api` und `/health`.

### Slice 4.2 — Dashboard Layout und Request-Liste

- HomePage durch AccessRequestDashboard ersetzen.
- AdminTokenPanel oben.
- Request-Liste mit Pending-Fokus, aber andere Status sichtbar machen.
- Empty/loading/error states.
- Auswahl eines Requests.

### Slice 4.3 — Detailansicht und Approve/Deny

- RequestDetails menschenlesbar.
- Comment-Feld.
- Approve/Deny Buttons.
- Token fehlt -> Buttons disabled oder klare Fehlermeldung.
- Nach Mutation Liste/Details invalidieren.
- Approval-Response mit SessionId anzeigen.

### Slice 4.4 — Session Summary und optionaler Dummy Action Smoke

- Session Details nach Approval laden oder anzeigen.
- Optionaler Button für erlaubte Dummy Action, wenn `test.echo` oder `test.status.read` in AllowedCapabilities enthalten ist.
- Result/Fehler sichtbar machen.

### Slice 4.5 — Tests und Validation

Frontend:

```bash
cd frontend
pnpm check
pnpm test -- --run
pnpm build
```

Backend smoke, falls Backend oder proxyrelevante Compose-Dateien geändert werden:

```bash
docker run --rm -u $(id -u):$(id -g) -e NUGET_PACKAGES=/tmp/nuget -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Gatekeeper.sln
```

Compose:

```bash
docker compose config
docker compose build frontend
```

## Commit Boundary

Phase 4 endet mit Commit und Push, wenn:

- UI zeigt pending Requests und Request Details.
- Approve/Deny funktionieren gegen bestehende Backend API.
- Admin Token wird nicht persistent gespeichert.
- API responses werden mit Zod validiert.
- Frontend check/test/build grün sind.
- Docker Compose frontend build grün ist.

Commit-Vorschlag:

```text
feat: add minimal approval web ui
```

## Agenten-Fallen

- Keine echte Auth in diese Phase ziehen.
- Kein Token in LocalStorage speichern.
- Keine API Responses casten; Zod parse verwenden.
- Keine Backend-Direktzugriffe aus Komponenten; React Query Hooks verwenden.
- Keine rohen JSON-Wände als Haupt-UX.
- Keine produktiven Adapter oder Raw Shell einführen.
