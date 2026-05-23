namespace Gatekeeper.Application.AuditEvents;

public sealed record AuditEventQueryCriteria(
    Guid? AggregateId,
    string? EventType,
    DateTimeOffset? From,
    DateTimeOffset? To,
    AuditEventCursor? Cursor,
    int Take
);
