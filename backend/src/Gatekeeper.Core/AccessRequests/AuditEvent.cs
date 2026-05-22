namespace Gatekeeper.Core.AccessRequests;

public sealed class AuditEvent
{
    private AuditEvent(
        Guid id,
        string eventType,
        Guid? aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        Id = id;
        EventType = eventType;
        AggregateId = aggregateId;
        OccurredAt = occurredAt;
        PayloadJson = payloadJson;
    }

    public Guid Id { get; }

    public string EventType { get; }

    public Guid? AggregateId { get; }

    public DateTimeOffset OccurredAt { get; }

    public string PayloadJson { get; }

    public static AuditEvent CreateAccessRequestCreated(
        Guid aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        if (aggregateId == Guid.Empty)
        {
            throw new ArgumentException("Aggregate id is required.", nameof(aggregateId));
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ArgumentException("Payload JSON is required.", nameof(payloadJson));
        }

        return new AuditEvent(
            Guid.NewGuid(),
            "AccessRequestCreated",
            aggregateId,
            occurredAt,
            payloadJson
        );
    }

    public static AuditEvent Load(
        Guid id,
        string eventType,
        Guid? aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        return new AuditEvent(id, eventType, aggregateId, occurredAt, payloadJson);
    }
}
