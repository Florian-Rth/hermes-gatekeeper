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
        return CreateForAggregate("AccessRequestCreated", aggregateId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateAccessRequestApproved(
        Guid aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("AccessRequestApproved", aggregateId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateAccessRequestDenied(
        Guid aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("AccessRequestDenied", aggregateId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateSessionCreated(
        Guid sessionId,
        Guid accessRequestId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        if (accessRequestId == Guid.Empty)
        {
            throw new ArgumentException("Access request id is required.", nameof(accessRequestId));
        }

        return CreateForAggregate("SessionCreated", sessionId, occurredAt, payloadJson);
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

    private static AuditEvent CreateForAggregate(
        string eventType,
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

        return new AuditEvent(Guid.NewGuid(), eventType, aggregateId, occurredAt, payloadJson);
    }
}
