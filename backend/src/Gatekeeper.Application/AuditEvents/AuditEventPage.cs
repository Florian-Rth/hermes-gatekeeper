namespace Gatekeeper.Application.AuditEvents;

public sealed record AuditEventPage(IReadOnlyList<AuditEventSummary> Items, string? NextCursor);
