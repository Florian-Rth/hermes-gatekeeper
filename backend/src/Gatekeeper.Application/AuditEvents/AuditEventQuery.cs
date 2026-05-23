namespace Gatekeeper.Application.AuditEvents;

public sealed record AuditEventQuery(
    Guid? AggregateId,
    string? EventType,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Cursor,
    int? Limit
);
