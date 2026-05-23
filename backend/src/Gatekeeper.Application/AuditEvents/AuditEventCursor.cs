namespace Gatekeeper.Application.AuditEvents;

public sealed record AuditEventCursor(DateTimeOffset OccurredAt, Guid Id);
