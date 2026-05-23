namespace Gatekeeper.Application.AuditEvents;

public sealed record AuditEventSummary(
    Guid Id,
    string EventType,
    Guid? AggregateId,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string> Details
);
