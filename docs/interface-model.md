# Hermes Gatekeeper — Hermes Interface Model

## Ziel

Hermes Gatekeeper soll für Hermes und andere Agenten über eine generische HTTP API bedienbar sein. Ein spezielles Hermes Toolset ist optional und kann später als Komfort-/Sicherheitslayer ergänzt werden.

## Entscheidung

Für den MVP wird das primäre Agent-Interface eine HTTP API.

Begründung:

- allgemein nutzbar, nicht nur für Hermes
- leichter Open Source deploybar
- funktioniert mit beliebigen Agenten, Scripts, CLIs und Automationen
- klar dokumentierbar über OpenAPI/Swagger
- passt gut zu .NET + FastEndpoints
- keine Hermes-spezifische Kopplung im Kernprodukt

## HTTP API vs. Hermes Toolset

### HTTP API

Eine HTTP API ist die eigentliche Gatekeeper-Schnittstelle.

Hermes kann sie z.B. per `curl`, Python, später eigenem Tool oder MCP nutzen.

Vorteile:

- generisch und interoperabel
- OpenAPI/Swagger-Dokumentation automatisch möglich
- andere Nutzer können eigene Clients bauen
- unabhängig von Hermes-Versionen
- einfacher als öffentliches Open-Source-Produkt zu verstehen
- gute Basis für Web UI, CLI, SDKs und spätere Integrationen

Nachteile:

- Hermes sieht ohne eigenes Tool nur generische HTTP/curl-Aufrufe
- weniger schöne Tool-Schemas im Agent-Kontext
- mehr Risiko, dass ein Agent Request-Payloads unstrukturiert baut, wenn keine Client-Library/Tool-Schicht existiert

### Hermes Toolset

Ein Hermes Toolset wäre eine Hermes-spezifische Integration, die intern die HTTP API aufruft.

Beispiel-Tools:

- `gatekeeper_request_access(...)`
- `gatekeeper_list_requests(...)`
- `gatekeeper_wait_for_approval(...)`
- `gatekeeper_execute_action(...)`
- `gatekeeper_complete_session(...)`

Vorteile:

- bessere Agent-Ergonomie
- stark typisierte Tool-Schemas für Hermes
- weniger Prompt-/Payload-Fehler
- sicherere Defaults möglich
- Gatekeeper-Aktionen erscheinen sauber als Toolcalls in Hermes

Nachteile:

- Hermes-spezifisch
- zusätzlicher Wartungsaufwand
- nicht nützlich für andere Agenten ohne Anpassung
- sollte nicht der Kern des Produkts sein

## Empfohlenes Modell

1. Gatekeeper Core ist immer HTTP API first.
2. Web UI nutzt dieselbe HTTP API.
3. Hermes nutzt am Anfang die HTTP API direkt.
4. Später bauen wir optional:
   - eine kleine CLI (`gatekeeper request ...`)
   - ein Hermes Toolset als Thin Wrapper
   - optional MCP Server für andere Agenten

## API Design Prinzipien

### 1. Agent-friendly, aber nicht agent-only

Requests sollen Felder haben, die für Agenten gut formulierbar und für Menschen gut prüfbar sind:

```json
{
  "intent": "Investigate why service is unhealthy",
  "targets": ["test-linux-vm"],
  "requestedCapabilities": ["system.status.read", "logs.read"],
  "durationMinutes": 15,
  "risk": "low",
  "justification": "User asked Hermes to debug service health.",
  "proposedActions": [
    "Read service status",
    "Read recent logs",
    "Check disk and memory usage"
  ],
  "forbiddenActions": [
    "Restart services",
    "Modify files",
    "Read secrets"
  ]
}
```

### 2. Stable public API

Da das Projekt Open Source werden soll, muss die API stabil und dokumentiert sein.

MVP sollte versioniert starten:

```text
/api/v1/access-requests
/api/v1/sessions
/api/v1/actions
```

### 3. OpenAPI als Vertrag

FastEndpoints kann OpenAPI/Swagger erzeugen. Diese Spezifikation wird der zentrale Vertrag für:

- Hermes
- Web UI
- CLI
- fremde Agenten
- Tests

### 4. Keine freien Shell-Befehle im generischen MVP

Die erste API soll keine Raw-Shell ausführen.

Stattdessen generische, aber typisierte Action-Klassen:

- `system.status.read`
- `logs.read`
- `service.status.read`
- `container.list`
- `container.logs.read`
- aktuell als erster kontrollierter Safe-Write-Slice: `service.restart`
- später: `container.restart`

## Minimaler API Flow

### 1. Access Request erstellen

```http
POST /api/v1/access-requests
```

Response:

```json
{
  "id": "req_123",
  "status": "pending",
  "approvalUrl": "https://gatekeeper.local/requests/req_123"
}
```

### 2. Hermes pollt oder wartet

```http
GET /api/v1/access-requests/req_123
```

Response vor Approval:

```json
{
  "id": "req_123",
  "status": "pending"
}
```

Response nach Approval:

```json
{
  "id": "req_123",
  "status": "approved",
  "sessionId": "sess_abc"
}
```

### 3. Genehmigte Action ausführen

```http
POST /api/v1/sessions/sess_abc/actions
```

Body:

```json
{
  "actionType": "logs.read",
  "target": "test-linux-vm",
  "parameters": {
    "source": "system",
    "lines": 100
  }
}
```

### 4. Session abschließen

```http
POST /api/v1/sessions/sess_abc/complete
```

Body:

```json
{
  "summary": "Read system logs and found no critical errors. No changes performed."
}
```

## Auth für Hermes API

Für den MVP:

- Hermes bekommt einen Gatekeeper Client Token oder mTLS Client Credential.
- Dieser Token erlaubt nur:
  - Requests erstellen
  - eigene Request-Status lesen
  - genehmigte Actions innerhalb eigener Sessions ausführen
  - eigene Sessions abschließen
- Dieser Token erlaubt nicht:
  - Requests genehmigen
  - Policies ändern
  - Zielsysteme verwalten
  - Audit löschen

Admin-Freigabe läuft getrennt über lokale Admin-Auth in der Web UI.

## Später mögliche Integrationen

### CLI

```bash
gatekeeper request --target test-linux-vm --cap logs.read --duration 15m
gatekeeper session exec sess_abc logs.read --target test-linux-vm --lines 100
```

### Hermes Toolset

Thin Wrapper um HTTP API. Kein eigener Security-Core.

### MCP Server

Ein MCP Server könnte Gatekeeper-Funktionen als Tools für beliebige MCP-fähige Agenten bereitstellen. Auch hier: MCP ist nur Adapter, HTTP API bleibt Kern.
