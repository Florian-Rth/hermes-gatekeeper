# Hermes Gatekeeper — Recherche zu bestehenden Systemen

Stand: 2026-05-22

## Fragestellung

Gibt es bereits ein System, das Hermes Gatekeeper weitgehend ersetzt?

Gesucht wurde nach Systemen für:

- Just-in-Time Access
- Approval Workflows
- Bastion / Infrastructure Access Proxy
- SSH-Zertifikate / temporäre Credentials
- Audit / Session Recording
- menschliche Freigabe für Agent-/Tool-Aktionen
- HomeLab-taugliche Open-Source-Lösungen

## Ergebnis in einem Satz

Es gibt starke Bausteine und Enterprise-Produkte für Just-in-Time Infrastructure Access, aber kein gefundenes System passt exakt auf unser Ziel: ein HomeLab-tauglicher, agent-spezifischer Capability Broker, der Hermes-Anfragen menschenlesbar genehmigt und danach typisierte, scope-begrenzte Aktionen gegen Home Assistant, VMs, Docker und später Proxmox ausführt.

Wir sollten also nicht blind alles selbst bauen, aber auch nicht erwarten, dass ein fertiges Tool unser komplettes Zielbild abdeckt.

## Kandidaten

| System | Kategorie | Fit | Bewertung |
|---|---|---:|---|
| Teleport | Zero-Trust Access / SSH / Kubernetes / DB / Apps | Hoch als Infrastruktur-Baustein | Sehr stark für JIT Access und Access Requests, aber volles Setup schwergewichtig; wichtige Access-Request-Funktionen sind Enterprise-lastig. |
| HashiCorp Boundary | Identity-aware Proxy / Bastion Alternative | Hoch als Infrastruktur-Baustein | Sehr passend für just-in-time Netzwerkzugriff, dynamische Credentials und Session Recording; weniger passend als agent-spezifischer Action Broker. |
| Common Fate | Internal Access Workflows / JIT Cloud Access | Mittel-Hoch als Approval-/Workflow-Inspiration | Sehr nah am Approval-/Policy-Gedanken, eher Cloud-/Enterprise-orientiert; interessant wegen Workflows, Cedar Policies, Slack/Webhook, Audit. |
| Pomerium | Identity-aware HTTP Proxy / Zero Trust | Mittel | Gut für Web-Apps und HTTP-basierte interne Dienste; weniger passend für SSH/HA/Docker Action Brokering. |
| Smallstep SSH / step-ca | SSH Certificate Authority | Mittel als Baustein | Sehr nützlich für temporäre SSH-Zertifikate; löst aber nicht Approval UI, Action Proxy und Agent-Scope alleine. |
| Cloudflare Access for Infrastructure | Zero Trust SSH / ephemeral SSH certs | Mittel | Technisch stark, aber Cloudflare-gebunden; für lokales HomeLab evtl. unnötig extern. |
| StrongDM / Apono / Opal / Indent etc. | Kommerzielle PAM/JIT Access Plattformen | Funktional hoch, praktisch niedrig | Können viel, aber für persönliches HomeLab vermutlich zu groß, teuer oder cloud-/enterprise-fokussiert. |
| HumanLayer / agent approval tools | Human-in-the-loop Agent Approval | Mittel als Konzept-Baustein | Relevant für Tool-Approval-Muster, aber nicht als HomeLab Infrastructure Broker. |
| EdgeAgent-Gateway / ähnliche MCP-Projekte | MCP/Agent Gateway mit Policies | Unklar | Interessante Richtung, aber aktuell nicht als reifer HomeLab-PAM-Ersatz bewertet. |

## Details

### Teleport

Quelle: https://goteleport.com/docs/identity-governance/access-requests/

Teleport bietet Just-in-Time Access Requests. Laut Doku können Benutzer temporären Zugriff auf Rollen oder Ressourcen anfragen; Requests können genehmigt oder abgelehnt werden. Teleport unterstützt Least Privilege, zeitlich begrenzte erhöhte Rechte und Dual Authorization.

Relevante Punkte:

- Access Requests
- Role und Resource Requests
- konfigurierbare Approver
- begrenzte Laufzeiten
- starke Infrastruktur-Abdeckung: SSH, Kubernetes, Datenbanken, Apps etc.
- Audit und Session Recording je nach Setup/Edition

Einschränkung:

- Voller Nutzen ist Enterprise-orientiert. Die Doku weist darauf hin, dass Just-in-Time Access Requests als Identity-Governance-Feature Enterprise sind; Community Edition hat nur eingeschränkte/Preview-artige Role Requests.
- Teleport ist ein komplettes Access-Plane-System, kein schlanker Agent-Capability-Broker.
- Für Home Assistant / typisierte Agent-Aktionen müsste trotzdem eigene Integration oder Mapping gebaut werden.

Bewertung:

Teleport könnte Gatekeeper teilweise ersetzen, wenn wir primär SSH/K8s/DB-Zugriff wollen und Enterprise/Komplexität akzeptieren. Für unser maßgeschneidertes Agent-Approval-System ist es eher Referenz und möglicher späterer Backend-Baustein.

### HashiCorp Boundary

Quelle: https://developer.hashicorp.com/boundary/docs/what-is-boundary

Boundary beschreibt sich als identity-aware proxy für least-privileged access zu Cloud-Infrastruktur. Es bietet laut Doku:

- SSO zu Targets
- Just-in-Time Network Access zu privaten Ressourcen
- dynamische Credentials via HashiCorp Vault
- Discovery von Zielsystemen
- Session Recording / privilegierte Sessions je nach Edition
- Zugriff ohne Credentials direkt an Nutzer zu verteilen

Relevante Punkte:

- sehr nah an unserem Proxy/Bastion-Teil
- guter Ersatz für „Hermes bekommt temporären Netzwerkzugriff“
- kann Credentials hinter einem Broker halten

Einschränkung:

- Boundary vermittelt primär Netzwerk-/Target-Zugriff, nicht typisierte HomeLab-Aktionen.
- Approval-Workflow und agentenspezifische Request-UI müssten geprüft/ergänzt werden.
- Für Home Assistant und sichere Operationen wie `read_service_logs` oder `restart_allowed_container` bräuchten wir trotzdem eigene Logik.

Bewertung:

Starker Kandidat als Infrastruktur-Baustein, aber nicht vollständiger Gatekeeper-Ersatz.

### Common Fate

Quelle: https://docs.commonfate.io/introduction

Common Fate beschreibt sich als Authorization Engine für internen Zugriff, die Cloud Access über zentralisierte Kontexte und Workflows automatisiert. Die Doku nennt:

- Access Workflows für sichere Zugriffe auf Produktionsumgebungen
- automatische und manuelle Approvals
- Routing über Slack
- Policies mit Kontextinformationen wie On-Call-Status, Ressourcenattribute und Gruppenmitgliedschaften
- Breakglass Access
- Audit Log Integrationen
- Self-hosting in eigener Cloud-Umgebung per Terraform

Relevante Punkte:

- gedanklich sehr nah an unserem Approval-/Policy-Teil
- interessant für Policy-Modell und Workflows
- Slack/Webhook/Audit als Inspiration

Einschränkung:

- Fokus scheint stärker auf Cloud Access / Enterprise-Workflows zu liegen.
- HomeLab, Home Assistant, SSH Action Proxy und Agent Tooling sind nicht direkt abgedeckt.
- Für unseren persönlichen Use Case vermutlich zu schwergewichtig.

Bewertung:

Sehr gute Inspirationsquelle für Workflows und Policies; wahrscheinlich kein direkter Drop-in-Ersatz.

### Pomerium

Quelle: https://www.pomerium.com/docs/

Pomerium ist ein Open-Source identity-aware proxy im BeyondCorp-/Zero-Trust-Stil. Es schützt interne Apps, Server, Services und Workloads durch Identitäts-, Geräte- und Kontextprüfung.

Relevante Punkte:

- sehr gut für HTTP/Web-App-Zugriff
- policies zentralisierbar
- HomeLab-tauglicher als viele Enterprise-PAM-Lösungen

Einschränkung:

- primär HTTP/Proxy-Zugriff, nicht Action Broker für SSH/HA/Docker.
- Approval/JIT-Workflow für Agent-Aktionen nicht Kernfunktion.

Bewertung:

Gut, wenn wir interne Web-Apps absichern wollen. Für Hermes Gatekeeper nur ein Baustein, nicht die zentrale Lösung.

### Smallstep SSH / step-ca

Quelle: https://smallstep.com/docs/ssh/

Smallstep/step-ca kann SSH-Zertifikate ausstellen. Die Doku beschreibt SSH Workflows mit OpenSSH, OAuth und SSH Certificates.

Relevante Punkte:

- gute technische Basis für kurzlebige SSH-Zertifikate
- könnte im Break-glass- oder Advanced-Modus verwendet werden
- Open-Source-Komponenten vorhanden (`step-ca`)

Einschränkung:

- löst nicht Genehmigungs-UI, Session Manager, Audit, Policy und typisierte Operationen.
- direkte SSH-Zertifikate geben Hermes mehr Freiheit als unser bevorzugter Broker-first-Ansatz.

Bewertung:

Guter Baustein für später, nicht MVP-Kern.

### Cloudflare Access for Infrastructure

Quelle: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/use-cases/ssh/ssh-infrastructure-access/

Cloudflare Access kann SSH-Zugriff über Cloudflare One absichern und langfristige SSH-Keys durch ephemere SSH-Zertifikate ersetzen.

Relevante Punkte:

- moderne Zero-Trust-SSH-Lösung
- ephemere Zertifikate
- kein klassischer VPN nötig

Einschränkung:

- externer Cloud-Anbieter im Zugriffspfad
- für persönliches, datenschutzbewusstes HomeLab möglicherweise nicht ideal
- kein spezifischer Agent-Action-Broker

Bewertung:

Technisch interessant, aber wahrscheinlich nicht passend als Kern für Florians Setup.

### Kommerzielle PAM/JIT-Plattformen

Beispiele: StrongDM, Apono, Opal Security, Indent, Britive, Teleport Enterprise.

Relevante Punkte:

- JIT Access
- Approval Workflows
- Audit
- Session Control
- teilweise Datenbanken, Kubernetes, Server, Cloud-Rollen

Einschränkung:

- meist Enterprise/SaaS
- potentiell teuer
- Overkill fürs HomeLab
- weniger Kontrolle/Privacy als selbstgehosteter Broker

Bewertung:

Gut als Referenz, aber wahrscheinlich nicht als Lösung.

### Agent-/MCP-spezifische Approval-Gateways

Bei GitHub-Suche tauchten kleinere Projekte in Richtung Agent Gateway / MCP / Human Approval auf, z.B. EdgeAgent-Gateway oder Jinguzhou. Beschreibungen nennen Policy Enforcement, Human Approval und Audit für Agent Tools.

Einschränkung:

- aktuell nicht als reif oder verbreitet bewertet
- unklarer Wartungsstand
- nicht offensichtlich für HomeLab/PAM/SSH/Home Assistant zugeschnitten

Bewertung:

Interessante Recherche-Richtung für später, aber kein Grund, Hermes Gatekeeper nicht zu konzipieren.

## Fazit

Es gibt drei Klassen von bestehenden Systemen:

1. **Infrastructure Access Planes** wie Teleport und Boundary
   - stark für SSH/K8s/DB/Apps
   - schwergewichtig
   - nicht agent-spezifisch genug

2. **Approval-/JIT-Workflow-Systeme** wie Common Fate
   - stark als Policy-/Workflow-Vorbild
   - eher Cloud/Enterprise-orientiert

3. **Identity-aware Proxies / SSH CA Bausteine** wie Pomerium, Cloudflare Access, Smallstep
   - nützlich für Teilprobleme
   - lösen nicht das ganze Zielbild

Hermes Gatekeeper bleibt sinnvoll, weil unser Ziel spezifischer ist:

- HomeLab-first
- selbstgehostet
- Agent-spezifisch
- Hermes-Anfragen menschenlesbar
- Capability-basiert
- typisierte Operationen statt freie Shell
- Home Assistant + SSH + Docker + später Proxmox
- keine dauerhaften Secrets bei Hermes
- bewusst kleine, nachvollziehbare Architektur

## Konsequenz für unser Design

Wir sollten nicht versuchen, Teleport/Boundary komplett nachzubauen.

Stattdessen:

1. MVP als schlanker Approval + Action Broker bauen.
2. Für SSH-Zugriff zunächst typisierte read-only Operationen über Broker.
3. Später prüfen, ob Boundary, Teleport oder Smallstep als Backend für bestimmte Access-Arten integriert werden.
4. Policy-/Workflow-Ideen von Common Fate übernehmen, aber einfacher halten.

## Nächste Recherche-Fragen

- Welche Auth-Lösung existiert bereits im HomeLab? Authentik/Authelia/Keycloak?
- Soll Gatekeeper komplett intern bleiben oder auch remote erreichbar sein?
- Gibt es schon zentrale SSH-User/Keys für VMs?
- Soll später Proxmox API eingebunden werden?
- Welche VM kann als ungefährliches erstes Target dienen?
