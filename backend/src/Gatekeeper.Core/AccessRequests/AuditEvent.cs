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

    public static AuditEvent CreateSessionCompleted(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("SessionCompleted", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateSessionRevoked(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("SessionRevoked", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateSessionExpired(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("SessionExpired", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateSessionActionRequested(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("SessionActionRequested", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateSessionActionAllowed(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("SessionActionAllowed", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateSessionActionDenied(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("SessionActionDenied", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateSessionActionExecuted(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("SessionActionExecuted", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateSessionActionFailed(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("SessionActionFailed", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateActionCountExceeded(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateForAggregate("ActionCountExceeded", sessionId, occurredAt, payloadJson);
    }

    public static AuditEvent CreateAdminLoginSucceeded(
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateSystem("AdminLoginSucceeded", occurredAt, payloadJson);
    }

    public static AuditEvent CreateAdminLoginFailed(DateTimeOffset occurredAt, string payloadJson)
    {
        return CreateSystem("AdminLoginFailed", occurredAt, payloadJson);
    }

    public static AuditEvent CreateAdminLogout(DateTimeOffset occurredAt, string payloadJson)
    {
        return CreateSystem("AdminLogout", occurredAt, payloadJson);
    }

    public static AuditEvent CreateAgentAuthenticationFailed(
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        return CreateSystem("AgentAuthenticationFailed", occurredAt, payloadJson);
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

    private static AuditEvent CreateSystem(
        string eventType,
        DateTimeOffset occurredAt,
        string payloadJson
    )
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ArgumentException("Payload JSON is required.", nameof(payloadJson));
        }

        return new AuditEvent(Guid.NewGuid(), eventType, null, occurredAt, payloadJson);
    }
}
