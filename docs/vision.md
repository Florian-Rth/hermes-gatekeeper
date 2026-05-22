# Hermes Gatekeeper — Vision

## Problem

Hermes Agent soll zukünftig nicht nur auf seiner eigenen VM arbeiten, sondern kontrolliert Aufgaben im gesamten HomeLab übernehmen können:

- andere VMs auf diesem Server
- ein weiterer Server
- Home Assistant
- später ggf. Proxmox, Docker Hosts, Services, Backups, Monitoring, Automationen

Diese Umgebungen sind produktiv und kritisch. Ein Agent mit dauerhaftem SSH-/API-Zugriff auf diese Systeme wäre zu riskant, insbesondere wegen Prompt Injection, Bedienfehlern, Modellfehlern oder kompromittierten externen Inhalten.

## Ziel

Hermes soll möglichst viel können, aber nicht dauerhaft alles dürfen.

Hermes Gatekeeper soll als allgemeines, selbsthostbares Open-Source-Projekt entstehen. Florians HomeLab ist der erste reale Use Case, aber das Produkt soll generisch genug sein, damit andere Nutzer es ebenfalls deployen und mit eigenen Agenten, Zielsystemen und Policies verwenden können.

Hermes Gatekeeper soll kontrollierte Capability Escalation ermöglichen:

1. Hermes hat standardmäßig keinen direkten Zugriff auf produktive Systeme außerhalb seiner VM.
2. Wenn Zugriff benötigt wird, erstellt Hermes eine strukturierte Genehmigungsanfrage.
3. Florian prüft diese Anfrage in einer Web-View.
4. Bei Genehmigung entsteht eine zeit-, scope- und/oder aufgabenbegrenzte Session.
5. Hermes kann in dieser Session nur die explizit freigegebenen Capabilities verwenden.
6. Alle Zugriffe und Aktionen werden auditiert.
7. Sessions können jederzeit widerrufen werden.

## Leitprinzipien

### 1. Capability statt globaler Zugriff

Hermes bekommt keine dauerhaften SSH-Keys, Home-Assistant-Tokens oder Proxmox-Credentials.

Stattdessen erhält Hermes kurzlebige, eng begrenzte Capabilities, z.B.:

- `ha.read.states`
- `ha.read.logs`
- `ssh.systemctl.status`
- `ssh.journalctl.read`
- `docker.logs.read`
- `docker.container.restart.allowed`

### 2. Proxy/Broker-first statt direkter Netzwerkfreigabe

Bevorzugte Richtung: Hermes spricht nicht direkt mit Produktivsystemen. Hermes spricht mit Gatekeeper. Gatekeeper führt genehmigte Aktionen gegen Zielsysteme aus.

Direkter temporärer SSH-/VPN-Zugriff bleibt optionaler Break-glass- oder Advanced-Modus, nicht der Standard.

### 3. Menschliche Freigabe für produktive Capabilities

Produktive Systeme erfordern explizite Freigabe durch Florian. Die UI muss nicht nur JSON anzeigen, sondern die Anfrage verständlich zusammenfassen:

- Wer fragt an?
- Welches Zielsystem?
- Welche Rechte?
- Wie lange?
- Welche konkreten Aktionen sind geplant?
- Welches Risiko?
- Was ist verboten?

### 4. Least Privilege pro Session

Jede Session ist begrenzt nach:

- Zielsystemen
- Operationen
- Laufzeit
- Anzahl Aktionen
- ggf. Pfaden, Services, Entitäten oder Containern

### 5. Read-only zuerst, aber generisch

Die ersten Integrationen sollen read-only sein und bewusst generisch bleiben. Das erste Ziel ist nicht Home Assistant, sondern ein generisches Target-/Capability-Modell, das später Home Assistant, SSH, Docker, Proxmox und andere Systeme gleichartig abbilden kann.

Erste generische Capability-Klassen:

- Status lesen
- Logs lesen
- Health Checks
- Ressourcen auflisten
- Metadaten lesen

Beispiele:

- `system.status.read`
- `logs.read`
- `service.status.read`
- `resource.list`
- `resource.metadata.read`

Schreibende Aktionen kommen später und brauchen stärkere Freigaben.

### 6. Keine Secrets bei Hermes

Gatekeeper hält oder vermittelt die echten Credentials zu produktiven Systemen. Hermes sieht höchstens kurzlebige Gatekeeper-Session-Tokens, aber keine echten Backend-Secrets.

### 7. Auditierbarkeit

Jede Anfrage, Entscheidung und Aktion wird protokolliert:

- Request Payload
- Genehmiger
- Zeitpunkt
- Session Scope
- Aktion
- Ziel
- Exit Code / Ergebnis
- Output Hash oder gekürzter Output
- Session-Ende / Revoke-Grund

### 8. Prompt-Injection-Resilienz durch technische Grenzen

Prompt Injection wird nicht primär durch Vertrauen ins Modell gelöst, sondern durch technische Grenzen:

- keine direkten Secrets
- keine direkte Shell als Standard
- erlaubte Operationen statt freie Befehle
- explizite menschliche Freigabe
- Policy Engine
- Audit und Revocation

## Nicht-Ziele für den Anfang

- Kein vollwertiges PAM-System ersetzen
- Kein generischer Remote-Desktop
- Keine dauerhafte Agent-Adminrolle im HomeLab
- Keine automatische Genehmigung für produktive Schreibaktionen
- Keine freie Root-Shell als Standardmodus

## Langfristiges Zielbild

Hermes kann im Alltag per CLI oder WhatsApp gefragt werden:

- „Warum ist Service X down?“
- „Schau bitte Home Assistant Logs an.“
- „Starte den Paperless Container neu.“
- „Prüfe Speicherplatz auf allen VMs.“
- „Warum hat die Automation nicht ausgelöst?“

Hermes stellt, falls nötig, eine Gatekeeper-Anfrage. Florian genehmigt gezielt. Hermes führt nur die freigegebenen Aktionen aus und fasst danach zusammen, was gemacht wurde.
