using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Tests;

public sealed class SessionServiceTests
{
    [Fact]
    public async Task CompleteAsync_Should_CompleteActiveSessionAndWriteAuditEvent_When_SessionIsActive()
    {
        var now = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(now, out var sessions, out var auditEvents, out var unitOfWork);
        Session session = CreateActiveSession(now.AddMinutes(-5), now.AddMinutes(25));
        sessions.Items.Add(session);

        SessionLifecycleResult result = await service.CompleteAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Success);
        Assert.NotNull(result.Session);
        Assert.Equal(SessionStatus.Completed, result.Session.Status);
        Assert.Equal(now, result.Session.CompletedAt);
        Assert.Equal(SessionStatus.Completed, sessions.Items.Single().Status);
        var auditEvent = Assert.Single(auditEvents.Items);
        Assert.Equal("SessionCompleted", auditEvent.EventType);
        Assert.Equal(session.Id, auditEvent.AggregateId);
        Assert.Equal(now, auditEvent.OccurredAt);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task RevokeAsync_Should_RevokeActiveSessionAndWriteAuditEvent_When_SessionIsActive()
    {
        var now = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(now, out var sessions, out var auditEvents, out var unitOfWork);
        Session session = CreateActiveSession(now.AddMinutes(-5), now.AddMinutes(25));
        sessions.Items.Add(session);

        SessionLifecycleResult result = await service.RevokeAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Success);
        Assert.NotNull(result.Session);
        Assert.Equal(SessionStatus.Revoked, result.Session.Status);
        Assert.Equal(now, result.Session.RevokedAt);
        Assert.Equal(SessionStatus.Revoked, sessions.Items.Single().Status);
        var auditEvent = Assert.Single(auditEvents.Items);
        Assert.Equal("SessionRevoked", auditEvent.EventType);
        Assert.Equal(session.Id, auditEvent.AggregateId);
        Assert.Equal(now, auditEvent.OccurredAt);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task GetByIdAsync_Should_MaterializeExpiredActiveSessionAndAuditOnlyOnce_When_SessionIsExpired()
    {
        var now = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(now, out var sessions, out var auditEvents, out var unitOfWork);
        Session session = CreateActiveSession(now.AddMinutes(-10), now.AddMinutes(-1));
        sessions.Items.Add(session);

        SessionDetails? first = await service.GetByIdAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );
        SessionDetails? second = await service.GetByIdAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(SessionStatus.Expired, first.Status);
        Assert.Equal(SessionStatus.Expired, second.Status);
        Assert.Equal(now, first.ExpiredAt);
        Assert.Equal(SessionStatus.Expired, sessions.Items.Single().Status);
        var auditEvent = Assert.Single(auditEvents.Items);
        Assert.Equal("SessionExpired", auditEvent.EventType);
        Assert.Equal(session.Id, auditEvent.AggregateId);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CompleteAsync_Should_ReturnConflictAfterMaterializingExpiry_When_SessionIsExpired()
    {
        var now = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(now, out var sessions, out var auditEvents, out var unitOfWork);
        Session session = CreateActiveSession(now.AddMinutes(-10), now.AddMinutes(-1));
        sessions.Items.Add(session);

        SessionLifecycleResult result = await service.CompleteAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Success);
        Assert.True(result.Conflict);
        Assert.NotNull(result.Session);
        Assert.Equal(SessionStatus.Expired, result.Session.Status);
        Assert.Equal(SessionStatus.Expired, sessions.Items.Single().Status);
        var auditEvent = Assert.Single(auditEvents.Items);
        Assert.Equal("SessionExpired", auditEvent.EventType);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task RevokeAsync_Should_ReturnConflictWithoutAuditOrSave_When_SessionIsTerminal()
    {
        var now = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(now, out var sessions, out var auditEvents, out var unitOfWork);
        sessions.Items.Add(
            CreateCompletedSession(now.AddMinutes(-5), now.AddMinutes(25), now.AddMinutes(-1))
        );

        SessionLifecycleResult result = await service.RevokeAsync(
            sessions.Items.Single().Id,
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Success);
        Assert.True(result.Conflict);
        Assert.Empty(auditEvents.Items);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CompleteAsync_Should_ReturnConflict_When_SaveDetectsStaleTerminalTransition()
    {
        var now = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(now, out var sessions, out _, out var unitOfWork);
        Session session = CreateActiveSession(now.AddMinutes(-5), now.AddMinutes(25));
        sessions.Items.Add(session);
        unitOfWork.ThrowConflictOnSave = true;

        SessionLifecycleResult result = await service.CompleteAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Success);
        Assert.True(result.Conflict);
        Assert.NotNull(result.Session);
        Assert.Equal(SessionStatus.Active, result.Session.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    private static SessionService CreateService(
        DateTimeOffset now,
        out FakeSessionRepository sessions,
        out FakeAuditEventRepository auditEvents,
        out FakeSessionUnitOfWork unitOfWork
    )
    {
        sessions = new FakeSessionRepository();
        auditEvents = new FakeAuditEventRepository();
        unitOfWork = new FakeSessionUnitOfWork();
        return new SessionService(sessions, auditEvents, unitOfWork, new FixedClock(now));
    }

    private static Session CreateActiveSession(DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        return Session.Load(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionStatus.Active,
            ["prod-api"],
            ["logs:read"],
            createdAt,
            expiresAt,
            0,
            10,
            null,
            null,
            null
        );
    }

    private static Session CreateCompletedSession(
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset completedAt
    )
    {
        return Session.Load(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionStatus.Completed,
            ["prod-api"],
            ["logs:read"],
            createdAt,
            expiresAt,
            0,
            10,
            completedAt,
            null,
            null
        );
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public List<Session> Items { get; } = [];

        public Task AddAsync(Session session, CancellationToken cancellationToken)
        {
            Items.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Session session, CancellationToken cancellationToken)
        {
            int index = Items.FindIndex(item => item.Id == session.Id);
            if (index >= 0)
            {
                Items[index] = session;
            }

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

    private sealed class FakeSessionUnitOfWork : ISessionActionUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public bool ThrowConflictOnSave { get; set; }

        public bool ReserveActionSlotResult { get; set; } = true;

        public Task<bool> TryReserveActionSlotAndSaveChangesAsync(
            Guid sessionId,
            DateTimeOffset reservationTime,
            AuditEvent auditEvent,
            CancellationToken cancellationToken
        )
        {
            SaveChangesCount++;
            return Task.FromResult(ReserveActionSlotResult);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            if (ThrowConflictOnSave)
            {
                throw new PersistenceConflictException("Conflict.");
            }

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
}
