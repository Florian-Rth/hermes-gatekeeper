namespace Gatekeeper.Core.AccessRequests;

public sealed class AuditEvent
{
    private AuditEvent(
        Guid id,
        string eventType,
        Guid? aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails
    )
    {
        Id = id;
        EventType = eventType;
        AggregateId = aggregateId;
        OccurredAt = occurredAt;
        PayloadJson = payloadJson;
        BoundedDetails = boundedDetails;
    }

    public Guid Id { get; }

    public string EventType { get; }

    public Guid? AggregateId { get; }

    public DateTimeOffset OccurredAt { get; }

    public string PayloadJson { get; }

    public IReadOnlyDictionary<string, string>? BoundedDetails { get; }

    public static AuditEvent CreateAccessRequestCreated(
        Guid aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "AccessRequestCreated",
            aggregateId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateAccessRequestApproved(
        Guid aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "AccessRequestApproved",
            aggregateId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateAccessRequestDenied(
        Guid aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "AccessRequestDenied",
            aggregateId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionCreated(
        Guid sessionId,
        Guid accessRequestId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        if (accessRequestId == Guid.Empty)
        {
            throw new ArgumentException("Access request id is required.", nameof(accessRequestId));
        }

        return CreateForAggregate(
            "SessionCreated",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionCompleted(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "SessionCompleted",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionRevoked(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "SessionRevoked",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionExpired(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "SessionExpired",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionActionRequested(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "SessionActionRequested",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionActionAllowed(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "SessionActionAllowed",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionActionDenied(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "SessionActionDenied",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionActionExecuted(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "SessionActionExecuted",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateSessionActionFailed(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "SessionActionFailed",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateActionCountExceeded(
        Guid sessionId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateForAggregate(
            "ActionCountExceeded",
            sessionId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    public static AuditEvent CreateAdminLoginSucceeded(
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateSystem("AdminLoginSucceeded", occurredAt, payloadJson, boundedDetails);
    }

    public static AuditEvent CreateAdminLoginFailed(
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateSystem("AdminLoginFailed", occurredAt, payloadJson, boundedDetails);
    }

    public static AuditEvent CreateAdminLogout(
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateSystem("AdminLogout", occurredAt, payloadJson, boundedDetails);
    }

    public static AuditEvent CreateAgentAuthenticationFailed(
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        return CreateSystem("AgentAuthenticationFailed", occurredAt, payloadJson, boundedDetails);
    }

    public static AuditEvent Load(
        Guid id,
        string eventType,
        Guid? aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails = null
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        return new AuditEvent(id, eventType, aggregateId, occurredAt, payloadJson, boundedDetails);
    }

    private static AuditEvent CreateForAggregate(
        string eventType,
        Guid aggregateId,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails
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
            eventType,
            aggregateId,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }

    private static AuditEvent CreateSystem(
        string eventType,
        DateTimeOffset occurredAt,
        string payloadJson,
        IReadOnlyDictionary<string, string>? boundedDetails
    )
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ArgumentException("Payload JSON is required.", nameof(payloadJson));
        }

        return new AuditEvent(
            Guid.NewGuid(),
            eventType,
            null,
            occurredAt,
            payloadJson,
            boundedDetails
        );
    }
}
