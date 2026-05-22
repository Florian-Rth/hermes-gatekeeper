using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Tests;

public sealed class AccessRequestDomainTests
{
    [Fact]
    public void Create_WithValidValues_CreatesPendingRequestWithTimestamps()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);

        var request = AccessRequest.Create(
            "Diagnose production incident",
            "alice@example.test",
            ["prod-api"],
            ["logs:read"],
            30,
            RiskLevel.Medium,
            "Incident response",
            ["inspect logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-123" },
            now
        );

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal("Diagnose production incident", request.Intent);
        Assert.Equal("alice@example.test", request.Requester);
        Assert.Equal(["prod-api"], request.Targets);
        Assert.Equal(["logs:read"], request.RequestedCapabilities);
        Assert.Equal(30, request.DurationMinutes);
        Assert.Equal(RiskLevel.Medium, request.Risk);
        Assert.Equal("Incident response", request.Justification);
        Assert.Equal(["inspect logs"], request.ProposedActions);
        Assert.Equal(["restart service"], request.ForbiddenActions);
        Assert.Equal("INC-123", request.Metadata["ticket"]);
        Assert.Equal(AccessRequestStatus.Pending, request.Status);
        Assert.Equal(now, request.CreatedAt);
        Assert.Equal(now, request.UpdatedAt);
    }

    [Theory]
    [InlineData("", "alice", new[] { "target" }, new[] { "capability" }, 30)]
    [InlineData(" ", "alice", new[] { "target" }, new[] { "capability" }, 30)]
    [InlineData("intent", "", new[] { "target" }, new[] { "capability" }, 30)]
    [InlineData("intent", " ", new[] { "target" }, new[] { "capability" }, 30)]
    [InlineData("intent", "alice", new string[] { }, new[] { "capability" }, 30)]
    [InlineData("intent", "alice", new[] { "target" }, new string[] { }, 30)]
    [InlineData("intent", "alice", new[] { "target" }, new[] { "capability" }, 0)]
    [InlineData("intent", "alice", new[] { "target" }, new[] { "capability" }, -1)]
    public void Create_WithInvalidRequiredValues_RejectsRequest(
        string intent,
        string requester,
        string[] targets,
        string[] requestedCapabilities,
        int durationMinutes
    )
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            AccessRequest.Create(
                intent,
                requester,
                targets,
                requestedCapabilities,
                durationMinutes,
                RiskLevel.Low,
                null,
                [],
                [],
                new Dictionary<string, string>(),
                now
            )
        );
    }

    [Fact]
    public void Create_WithInvalidRisk_RejectsRequest()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AccessRequest.Create(
                "intent",
                "requester",
                ["target"],
                ["capability"],
                30,
                (RiskLevel)999,
                null,
                [],
                [],
                new Dictionary<string, string>(),
                now
            )
        );
    }

    [Fact]
    public void Approve_WithPendingRequest_ReturnsApprovedRequestAndUpdatesUpdatedAt()
    {
        var createdAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var approvedAt = createdAt.AddMinutes(5);
        var request = CreateRequest(createdAt);

        var approved = request.Approve(approvedAt);

        Assert.Equal(request.Id, approved.Id);
        Assert.Equal(AccessRequestStatus.Approved, approved.Status);
        Assert.Equal(createdAt, approved.CreatedAt);
        Assert.Equal(approvedAt, approved.UpdatedAt);
        Assert.Equal(request.Intent, approved.Intent);
        Assert.Equal(request.Requester, approved.Requester);
        Assert.Equal(request.Targets, approved.Targets);
        Assert.Equal(request.RequestedCapabilities, approved.RequestedCapabilities);
        Assert.Equal(request.DurationMinutes, approved.DurationMinutes);
        Assert.Equal(request.Risk, approved.Risk);
        Assert.Equal(request.Justification, approved.Justification);
        Assert.Equal(request.ProposedActions, approved.ProposedActions);
        Assert.Equal(request.ForbiddenActions, approved.ForbiddenActions);
        Assert.Equal(request.Metadata, approved.Metadata);
    }

    [Fact]
    public void Deny_WithPendingRequest_ReturnsDeniedRequestAndUpdatesUpdatedAt()
    {
        var createdAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var deniedAt = createdAt.AddMinutes(5);
        var request = CreateRequest(createdAt);

        var denied = request.Deny(deniedAt);

        Assert.Equal(request.Id, denied.Id);
        Assert.Equal(AccessRequestStatus.Denied, denied.Status);
        Assert.Equal(createdAt, denied.CreatedAt);
        Assert.Equal(deniedAt, denied.UpdatedAt);
    }

    [Theory]
    [InlineData(AccessRequestStatus.Approved)]
    [InlineData(AccessRequestStatus.Denied)]
    public void ApproveOrDeny_WithNonPendingRequest_RejectsTransition(AccessRequestStatus status)
    {
        var createdAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var request = AccessRequest.Load(
            Guid.NewGuid(),
            "intent",
            "requester",
            ["target"],
            ["capability"],
            30,
            RiskLevel.Low,
            null,
            [],
            [],
            new Dictionary<string, string>(),
            status,
            createdAt,
            createdAt
        );

        Assert.Throws<InvalidOperationException>(() => request.Approve(createdAt.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => request.Deny(createdAt.AddMinutes(1)));
    }

    [Fact]
    public void CreateFromApprovedAccessRequest_CarriesAllowlistsAndExpiry()
    {
        var createdAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var now = createdAt.AddMinutes(5);
        var approvedRequest = CreateRequest(createdAt).Approve(now);

        var session = Session.CreateFromApprovedAccessRequest(approvedRequest, now);

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(approvedRequest.Id, session.AccessRequestId);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(approvedRequest.Targets, session.AllowedTargets);
        Assert.Equal(approvedRequest.RequestedCapabilities, session.AllowedCapabilities);
        Assert.Equal(now, session.CreatedAt);
        Assert.Equal(now.AddMinutes(approvedRequest.DurationMinutes), session.ExpiresAt);
    }

    [Fact]
    public void CreateFromApprovedAccessRequest_WithPendingRequest_RejectsSessionCreation()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var request = CreateRequest(now);

        Assert.Throws<InvalidOperationException>(() =>
            Session.CreateFromApprovedAccessRequest(request, now)
        );
    }

    [Fact]
    public void CreateAccessRequestCreated_CreatesAuditEvent()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var aggregateId = Guid.NewGuid();

        var auditEvent = AuditEvent.CreateAccessRequestCreated(
            aggregateId,
            occurredAt,
            "{\"id\":\"request\"}"
        );

        Assert.NotEqual(Guid.Empty, auditEvent.Id);
        Assert.Equal("AccessRequestCreated", auditEvent.EventType);
        Assert.Equal(aggregateId, auditEvent.AggregateId);
        Assert.Equal(occurredAt, auditEvent.OccurredAt);
        Assert.Equal("{\"id\":\"request\"}", auditEvent.PayloadJson);
    }

    [Theory]
    [InlineData("AccessRequestApproved")]
    [InlineData("AccessRequestDenied")]
    public void AccessRequestTransitionFactories_CreateAuditEvent(string eventType)
    {
        var occurredAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var aggregateId = Guid.NewGuid();
        var payloadJson = "{\"id\":\"request\"}";

        var auditEvent =
            eventType == "AccessRequestApproved"
                ? AuditEvent.CreateAccessRequestApproved(aggregateId, occurredAt, payloadJson)
                : AuditEvent.CreateAccessRequestDenied(aggregateId, occurredAt, payloadJson);

        Assert.NotEqual(Guid.Empty, auditEvent.Id);
        Assert.Equal(eventType, auditEvent.EventType);
        Assert.Equal(aggregateId, auditEvent.AggregateId);
        Assert.Equal(occurredAt, auditEvent.OccurredAt);
        Assert.Equal(payloadJson, auditEvent.PayloadJson);
    }

    [Fact]
    public void CreateSessionCreated_CreatesAuditEvent()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var sessionId = Guid.NewGuid();
        var accessRequestId = Guid.NewGuid();
        var payloadJson = "{\"id\":\"session\"}";

        var auditEvent = AuditEvent.CreateSessionCreated(
            sessionId,
            accessRequestId,
            occurredAt,
            payloadJson
        );

        Assert.NotEqual(Guid.Empty, auditEvent.Id);
        Assert.Equal("SessionCreated", auditEvent.EventType);
        Assert.Equal(sessionId, auditEvent.AggregateId);
        Assert.Equal(occurredAt, auditEvent.OccurredAt);
        Assert.Equal(payloadJson, auditEvent.PayloadJson);
    }

    private static AccessRequest CreateRequest(DateTimeOffset now)
    {
        return AccessRequest.Create(
            "Diagnose production incident",
            "alice@example.test",
            ["prod-api", "prod-db"],
            ["logs:read", "metrics:read"],
            30,
            RiskLevel.Medium,
            "Incident response",
            ["inspect logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-123" },
            now
        );
    }
}
