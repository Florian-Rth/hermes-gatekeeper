using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Tests;

public sealed class AccessRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsPendingRequestAndWritesAuditEvent()
    {
        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var accessRequests = new FakeAccessRequestRepository();
        var auditEvents = new FakeAuditEventRepository();
        var service = new AccessRequestService(
            accessRequests,
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
        var service = new AccessRequestService(
            accessRequests,
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
        var service = new AccessRequestService(
            accessRequests,
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

    private sealed class FakeAccessRequestRepository : IAccessRequestRepository
    {
        public List<AccessRequest> Items { get; } = [];

        public Task AddAsync(AccessRequest accessRequest, CancellationToken cancellationToken)
        {
            Items.Add(accessRequest);
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
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
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
