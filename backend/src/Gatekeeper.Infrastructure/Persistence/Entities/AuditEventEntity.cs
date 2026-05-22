namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class AuditEventEntity
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public Guid? AggregateId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string PayloadJson { get; set; } = string.Empty;
}
