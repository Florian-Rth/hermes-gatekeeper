using Gatekeeper.Core.AccessRequests;

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
}
