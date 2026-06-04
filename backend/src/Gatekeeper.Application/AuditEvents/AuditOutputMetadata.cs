namespace Gatekeeper.Application.AuditEvents;

public sealed record AuditOutputMetadata(int? StdoutBytes, int? StderrBytes);
