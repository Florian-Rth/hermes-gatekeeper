using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Tests;

public sealed class AccessRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsPendingRequestAndWritesAuditEvent()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var accessRequests = new FakeAccessRequestRepository();
        var sessions = new FakeSessionRepository();
        var auditEvents = new FakeAuditEventRepository();
        var service = new AccessRequestService(
            accessRequests,
            sessions,
            new FakeAccessRequestUnitOfWork(),
            auditEvents,
            new FixedClock(now)
        );
        var command = CreateCommand("Diagnose production incident");

        var result = await service.CreateAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(AccessRequestStatus.Pending, result.Status);
        Assert.Equal(now, result.CreatedAt);
        Assert.Equal(now, result.UpdatedAt);
        var persisted = Assert.Single(accessRequests.Items);
        Assert.Equal(result.Id, persisted.Id);
        Assert.Equal(AccessRequestStatus.Pending, persisted.Status);
        var auditEvent = Assert.Single(auditEvents.Items);
        Assert.Equal("AccessRequestCreated", auditEvent.EventType);
        Assert.Equal(result.Id, auditEvent.AggregateId);
        Assert.Equal(now, auditEvent.OccurredAt);
        Assert.Contains(result.Id.ToString(), auditEvent.PayloadJson);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsStoredRequestOrNull()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var accessRequests = new FakeAccessRequestRepository();
        var sessions = new FakeSessionRepository();
        var service = new AccessRequestService(
            accessRequests,
            sessions,
            new FakeAccessRequestUnitOfWork(),
            new FakeAuditEventRepository(),
            new FixedClock(now)
        );
        var created = await service.CreateAsync(
            CreateCommand("Investigate issue"),
            TestContext.Current.CancellationToken
        );

        var found = await service.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);
        var missing = await service.GetByIdAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("Investigate issue", found.Intent);
        Assert.Null(missing);
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestRequestsFirst()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero));
        var accessRequests = new FakeAccessRequestRepository();
        var sessions = new FakeSessionRepository();
        var service = new AccessRequestService(
            accessRequests,
            sessions,
            new FakeAccessRequestUnitOfWork(),
            new FakeAuditEventRepository(),
            clock
        );
        var oldest = await service.CreateAsync(
            CreateCommand("Oldest"),
            TestContext.Current.CancellationToken
        );
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var newest = await service.CreateAsync(
            CreateCommand("Newest"),
            TestContext.Current.CancellationToken
        );

        var result = await service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(newest.Id, result[0].Id);
        Assert.Equal(oldest.Id, result[1].Id);
        Assert.Equal("Newest", result[0].Intent);
        Assert.Equal("Oldest", result[1].Intent);
    }

    [Fact]
    public async Task ApproveAsync_CreatesActiveSessionAndWritesAuditEvents()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            out var accessRequests,
            out var sessions,
            out var auditEvents,
            out var unitOfWork,
            now
        );
        var request = CreateAccessRequest(now.AddMinutes(-5));
        accessRequests.Items.Add(request);

        var result = await service.ApproveAsync(
            new ApproveAccessRequestCommand(request.Id, "looks good"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Success);
        Assert.False(result.NotFound);
        Assert.False(result.Conflict);
        Assert.NotNull(result.AccessRequest);
        Assert.NotNull(result.Session);
        Assert.Equal(AccessRequestStatus.Approved, result.AccessRequest.Status);
        Assert.Equal(now, result.AccessRequest.UpdatedAt);
        Assert.Equal(request.Id, result.Session.AccessRequestId);
        Assert.Equal(SessionStatus.Active, result.Session.Status);
        Assert.Equal(["prod-api"], result.Session.AllowedTargets);
        Assert.Equal(["logs:read"], result.Session.AllowedCapabilities);
        Assert.Equal(now, result.Session.CreatedAt);
        Assert.Equal(now.AddMinutes(30), result.Session.ExpiresAt);
        var persistedRequest = Assert.Single(accessRequests.Items);
        Assert.Equal(AccessRequestStatus.Approved, persistedRequest.Status);
        var persistedSession = Assert.Single(sessions.Items);
        Assert.Equal(result.Session.Id, persistedSession.Id);
        Assert.Equal(2, auditEvents.Items.Count);
        Assert.Contains(
            auditEvents.Items,
            item =>
                item.EventType == "AccessRequestApproved"
                && item.AggregateId == request.Id
                && item.PayloadJson.Contains("looks good")
        );
        Assert.Contains(
            auditEvents.Items,
            item => item.EventType == "SessionCreated" && item.AggregateId == result.Session.Id
        );
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task DenyAsync_WritesAuditEventAndCreatesNoSession()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            out var accessRequests,
            out var sessions,
            out var auditEvents,
            out var unitOfWork,
            now
        );
        var request = CreateAccessRequest(now.AddMinutes(-5));
        accessRequests.Items.Add(request);

        var result = await service.DenyAsync(
            new DenyAccessRequestCommand(request.Id, "too risky"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Success);
        Assert.False(result.NotFound);
        Assert.False(result.Conflict);
        Assert.NotNull(result.AccessRequest);
        Assert.Equal(AccessRequestStatus.Denied, result.AccessRequest.Status);
        Assert.Empty(sessions.Items);
        var persistedRequest = Assert.Single(accessRequests.Items);
        Assert.Equal(AccessRequestStatus.Denied, persistedRequest.Status);
        var auditEvent = Assert.Single(auditEvents.Items);
        Assert.Equal("AccessRequestDenied", auditEvent.EventType);
        Assert.Equal(request.Id, auditEvent.AggregateId);
        Assert.Contains("too risky", auditEvent.PayloadJson);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ApproveAsync_ReturnsNotFoundForMissingRequest()
    {
        var service = CreateService(
            out _,
            out _,
            out _,
            out var unitOfWork,
            new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero)
        );

        var result = await service.ApproveAsync(
            new ApproveAccessRequestCommand(Guid.NewGuid(), null),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Success);
        Assert.True(result.NotFound);
        Assert.False(result.Conflict);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task DenyAsync_ReturnsNotFoundForMissingRequest()
    {
        var service = CreateService(
            out _,
            out _,
            out _,
            out var unitOfWork,
            new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero)
        );

        var result = await service.DenyAsync(
            new DenyAccessRequestCommand(Guid.NewGuid(), null),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Success);
        Assert.True(result.NotFound);
        Assert.False(result.Conflict);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ApproveAsync_ReturnsConflictForNonPendingRequest()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            out var accessRequests,
            out var sessions,
            out var auditEvents,
            out var unitOfWork,
            now
        );
        var request = CreateAccessRequest(now).Approve(now);
        accessRequests.Items.Add(request);

        var result = await service.ApproveAsync(
            new ApproveAccessRequestCommand(request.Id, null),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Success);
        Assert.False(result.NotFound);
        Assert.True(result.Conflict);
        Assert.Empty(sessions.Items);
        Assert.Empty(auditEvents.Items);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task DenyAsync_ReturnsConflictForNonPendingRequest()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            out var accessRequests,
            out var sessions,
            out var auditEvents,
            out var unitOfWork,
            now
        );
        var request = CreateAccessRequest(now).Deny(now);
        accessRequests.Items.Add(request);

        var result = await service.DenyAsync(
            new DenyAccessRequestCommand(request.Id, null),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Success);
        Assert.False(result.NotFound);
        Assert.True(result.Conflict);
        Assert.Empty(sessions.Items);
        Assert.Empty(auditEvents.Items);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    private static CreateAccessRequestCommand CreateCommand(string intent)
    {
        return new CreateAccessRequestCommand(
            intent,
            "alice@example.test",
            ["prod-api"],
            ["logs:read"],
            30,
            RiskLevel.Low,
            "Incident response",
            ["inspect logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-123" }
        );
    }

    private static AccessRequest CreateAccessRequest(DateTimeOffset now)
    {
        return AccessRequest.Create(
            "Diagnose production incident",
            "alice@example.test",
            ["prod-api"],
            ["logs:read"],
            30,
            RiskLevel.Low,
            "Incident response",
            ["inspect logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-123" },
            now
        );
    }

    private static AccessRequestService CreateService(
        out FakeAccessRequestRepository accessRequests,
        out FakeSessionRepository sessions,
        out FakeAuditEventRepository auditEvents,
        out FakeAccessRequestUnitOfWork unitOfWork,
        DateTimeOffset now
    )
    {
        accessRequests = new FakeAccessRequestRepository();
        sessions = new FakeSessionRepository();
        auditEvents = new FakeAuditEventRepository();
        unitOfWork = new FakeAccessRequestUnitOfWork();
        return new AccessRequestService(
            accessRequests,
            sessions,
            unitOfWork,
            auditEvents,
            new FixedClock(now)
        );
    }

    private sealed class FakeAccessRequestRepository : IAccessRequestRepository
    {
        public List<AccessRequest> Items { get; } = [];

        public Task AddAsync(AccessRequest accessRequest, CancellationToken cancellationToken)
        {
            Items.Add(accessRequest);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AccessRequest accessRequest, CancellationToken cancellationToken)
        {
            var index = Items.FindIndex(item => item.Id == accessRequest.Id);
            if (index >= 0)
            {
                Items[index] = accessRequest;
            }

            return Task.CompletedTask;
        }

        public Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        }

        public Task<IReadOnlyList<AccessRequest>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AccessRequest>>(Items);
        }
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public List<Session> Items { get; } = [];

        public Task AddAsync(Session session, CancellationToken cancellationToken)
        {
            Items.Add(session);
            return Task.CompletedTask;
        }

        public Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        }
    }

    private sealed class FakeAuditEventRepository : IAuditEventRepository
    {
        public List<AuditEvent> Items { get; } = [];

        public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Items.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccessRequestUnitOfWork : IAccessRequestUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class MutableClock : IClock
    {
        public MutableClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }
    }
}
