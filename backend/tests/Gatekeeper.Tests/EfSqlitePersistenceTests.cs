using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Gatekeeper.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Tests;

public sealed class EfSqlitePersistenceTests
{
    [Fact]
    public async Task MigrationsApplyAndRequestAndAuditRoundtrip()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        await using GatekeeperDbContext dbContext = new GatekeeperDbContext(options);
        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var now = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var service = new AccessRequestService(
            new EfAccessRequestRepository(dbContext),
            new EfSessionRepository(dbContext),
            new EfAccessRequestUnitOfWork(dbContext),
            new EfAuditEventRepository(dbContext),
            new FixedClock(now)
        );

        AccessRequestDetails created = await service.CreateAsync(
            new CreateAccessRequestCommand(
                "Investigate production incident",
                "agent-1",
                ["prod-api"],
                ["read_logs"],
                30,
                RiskLevel.Medium,
                "Need diagnosis",
                ["tail logs"],
                ["restart service"],
                new Dictionary<string, string> { ["ticket"] = "INC-1" }
            ),
            TestContext.Current.CancellationToken
        );

        AccessRequestDetails? found = await service.GetByIdAsync(
            created.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("Investigate production incident", found.Intent);
        Assert.Equal("agent-1", found.Requester);
        Assert.Equal(["prod-api"], found.Targets);
        Assert.Equal(["read_logs"], found.RequestedCapabilities);
        Assert.Equal(["tail logs"], found.ProposedActions);
        Assert.Equal(["restart service"], found.ForbiddenActions);
        Assert.Equal("INC-1", found.Metadata["ticket"]);

        int auditCount = await dbContext.AuditEvents.CountAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, auditCount);
    }

    [Fact]
    public async Task AccessRequestStatusUpdateRoundtrips()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(5);
        var request = AccessRequest.Create(
            "Investigate production incident",
            "agent-1",
            ["prod-api"],
            ["read_logs"],
            30,
            RiskLevel.Medium,
            "Need diagnosis",
            ["tail logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-1" },
            createdAt
        );

        await using (GatekeeperDbContext writeContext = new GatekeeperDbContext(options))
        {
            await writeContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var repository = new EfAccessRequestRepository(writeContext);
            await repository.AddAsync(request, TestContext.Current.CancellationToken);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            await repository.UpdateAsync(
                request.Deny(updatedAt),
                TestContext.Current.CancellationToken
            );
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using GatekeeperDbContext readContext = new GatekeeperDbContext(options);
        AccessRequest? found = await new EfAccessRequestRepository(readContext).GetByIdAsync(
            request.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(found);
        Assert.Equal(AccessRequestStatus.Denied, found.Status);
        Assert.Equal(updatedAt, found.UpdatedAt);
    }

    [Fact]
    public async Task ApproveAsyncPersistsRequestSessionAndAuditEventsTogether()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 22, 11, 55, 0, TimeSpan.Zero);
        var approvedAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var request = AccessRequest.Create(
            "Investigate production incident",
            "agent-1",
            ["prod-api"],
            ["read_logs"],
            30,
            RiskLevel.Medium,
            "Need diagnosis",
            ["tail logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-1" },
            createdAt
        );

        await using (GatekeeperDbContext seedContext = new GatekeeperDbContext(options))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await new EfAccessRequestRepository(seedContext).AddAsync(
                request,
                TestContext.Current.CancellationToken
            );
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        ApprovalResult result;
        await using (GatekeeperDbContext approveContext = new GatekeeperDbContext(options))
        {
            var service = new AccessRequestService(
                new EfAccessRequestRepository(approveContext),
                new EfSessionRepository(approveContext),
                new EfAccessRequestUnitOfWork(approveContext),
                new EfAuditEventRepository(approveContext),
                new FixedClock(approvedAt)
            );

            result = await service.ApproveAsync(
                new ApproveAccessRequestCommand(request.Id, "approved"),
                TestContext.Current.CancellationToken
            );
        }

        Assert.True(result.Success);
        Assert.NotNull(result.Session);

        await using GatekeeperDbContext readContext = new GatekeeperDbContext(options);
        AccessRequest? persistedRequest = await new EfAccessRequestRepository(
            readContext
        ).GetByIdAsync(request.Id, TestContext.Current.CancellationToken);
        Session? persistedSession = await new EfSessionRepository(readContext).GetByIdAsync(
            result.Session.Id,
            TestContext.Current.CancellationToken
        );
        int auditCount = await readContext.AuditEvents.CountAsync(
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(persistedRequest);
        Assert.Equal(AccessRequestStatus.Approved, persistedRequest.Status);
        Assert.NotNull(persistedSession);
        Assert.Equal(request.Id, persistedSession.AccessRequestId);
        Assert.Equal(SessionStatus.Active, persistedSession.Status);
        Assert.Equal(["prod-api"], persistedSession.AllowedTargets);
        Assert.Equal(["read_logs"], persistedSession.AllowedCapabilities);
        Assert.Equal(0, persistedSession.ActionCount);
        Assert.Equal(10, persistedSession.MaxActionCount);
        Assert.Equal(2, auditCount);
    }

    [Fact]
    public async Task StalePendingDenyAfterApprovalIsRejectedAndDoesNotOverwriteApproval()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 22, 11, 55, 0, TimeSpan.Zero);
        var approvedAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var deniedAt = new DateTimeOffset(2026, 5, 22, 12, 1, 0, TimeSpan.Zero);
        var request = AccessRequest.Create(
            "Investigate production incident",
            "agent-1",
            ["prod-api"],
            ["read_logs"],
            30,
            RiskLevel.Medium,
            "Need diagnosis",
            ["tail logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-1" },
            createdAt
        );

        await using (GatekeeperDbContext seedContext = new GatekeeperDbContext(options))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await new EfAccessRequestRepository(seedContext).AddAsync(
                request,
                TestContext.Current.CancellationToken
            );
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AccessRequest staleApproval;
        AccessRequest staleDenial;
        await using GatekeeperDbContext approveContext = new GatekeeperDbContext(options);
        await using GatekeeperDbContext denyContext = new GatekeeperDbContext(options);
        var approveRequests = new EfAccessRequestRepository(approveContext);
        var denyRequests = new EfAccessRequestRepository(denyContext);

        staleApproval = (
            await approveRequests.GetByIdAsync(request.Id, TestContext.Current.CancellationToken)
        )!;
        staleDenial = (
            await denyRequests.GetByIdAsync(request.Id, TestContext.Current.CancellationToken)
        )!;

        AccessRequest approved = staleApproval.Approve(approvedAt);
        await approveRequests.UpdateAsync(approved, TestContext.Current.CancellationToken);
        await new EfSessionRepository(approveContext).AddAsync(
            Session.CreateFromApprovedAccessRequest(approved, approvedAt),
            TestContext.Current.CancellationToken
        );
        await new EfAccessRequestUnitOfWork(approveContext).SaveChangesAsync(
            TestContext.Current.CancellationToken
        );

        await denyRequests.UpdateAsync(
            staleDenial.Deny(deniedAt),
            TestContext.Current.CancellationToken
        );
        await Assert.ThrowsAsync<PersistenceConflictException>(() =>
            new EfAccessRequestUnitOfWork(denyContext).SaveChangesAsync(
                TestContext.Current.CancellationToken
            )
        );

        await using GatekeeperDbContext readContext = new GatekeeperDbContext(options);
        AccessRequest? persistedRequest = await new EfAccessRequestRepository(
            readContext
        ).GetByIdAsync(request.Id, TestContext.Current.CancellationToken);
        int sessionCount = await readContext.Sessions.CountAsync(
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(persistedRequest);
        Assert.Equal(AccessRequestStatus.Approved, persistedRequest.Status);
        Assert.Equal(approvedAt, persistedRequest.UpdatedAt);
        Assert.Equal(1, sessionCount);
    }

    [Fact]
    public async Task SessionLoadByIdRoundtrips()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var request = AccessRequest.Create(
            "Investigate production incident",
            "agent-1",
            ["prod-api"],
            ["read_logs"],
            30,
            RiskLevel.Medium,
            "Need diagnosis",
            ["tail logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-1" },
            createdAt
        );
        var session = Session.CreateFromApprovedAccessRequest(
            request.Approve(createdAt),
            createdAt
        );

        await using (GatekeeperDbContext writeContext = new GatekeeperDbContext(options))
        {
            await writeContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await new EfSessionRepository(writeContext).AddAsync(
                session,
                TestContext.Current.CancellationToken
            );
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using GatekeeperDbContext readContext = new GatekeeperDbContext(options);
        Session? found = await new EfSessionRepository(readContext).GetByIdAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(found);
        Assert.Equal(session.Id, found.Id);
        Assert.Equal(request.Id, found.AccessRequestId);
        Assert.Equal(SessionStatus.Active, found.Status);
        Assert.Equal(["prod-api"], found.AllowedTargets);
        Assert.Equal(["read_logs"], found.AllowedCapabilities);
        Assert.Equal(createdAt, found.CreatedAt);
        Assert.Equal(createdAt.AddMinutes(30), found.ExpiresAt);
        Assert.Equal(0, found.ActionCount);
        Assert.Equal(10, found.MaxActionCount);
        Assert.Null(found.CompletedAt);
        Assert.Null(found.RevokedAt);
        Assert.Null(found.ExpiredAt);
    }

    [Fact]
    public async Task SessionRepository_Should_RoundtripLifecycleFields_When_TerminalSessionHasActionBudget()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var completedAt = createdAt.AddMinutes(10);
        var session = Session.Load(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionStatus.Completed,
            ["prod-api"],
            ["read_logs"],
            createdAt,
            createdAt.AddMinutes(30),
            actionCount: 4,
            maxActionCount: 7,
            completedAt: completedAt,
            revokedAt: null,
            expiredAt: null
        );

        await using (GatekeeperDbContext writeContext = new GatekeeperDbContext(options))
        {
            await writeContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await new EfSessionRepository(writeContext).AddAsync(
                session,
                TestContext.Current.CancellationToken
            );
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using GatekeeperDbContext readContext = new GatekeeperDbContext(options);
        Session? found = await new EfSessionRepository(readContext).GetByIdAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(found);
        Assert.Equal(SessionStatus.Completed, found.Status);
        Assert.Equal(4, found.ActionCount);
        Assert.Equal(7, found.MaxActionCount);
        Assert.Equal(completedAt, found.CompletedAt);
        Assert.Null(found.RevokedAt);
        Assert.Null(found.ExpiredAt);
    }

    [Fact]
    public async Task SessionRepository_Should_RejectStaleTerminalTransition_When_StatusChangedInAnotherContext()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        Session session = Session.Load(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionStatus.Active,
            ["prod-api"],
            ["read_logs"],
            createdAt,
            createdAt.AddMinutes(30),
            actionCount: 0,
            maxActionCount: 10,
            completedAt: null,
            revokedAt: null,
            expiredAt: null
        );

        await using (GatekeeperDbContext seedContext = new GatekeeperDbContext(options))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await new EfSessionRepository(seedContext).AddAsync(
                session,
                TestContext.Current.CancellationToken
            );
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using GatekeeperDbContext firstContext = new GatekeeperDbContext(options);
        await using GatekeeperDbContext staleContext = new GatekeeperDbContext(options);
        var firstSessions = new EfSessionRepository(firstContext);
        var staleSessions = new EfSessionRepository(staleContext);
        var firstAudits = new EfAuditEventRepository(firstContext);
        var staleAudits = new EfAuditEventRepository(staleContext);
        Session firstLoaded = (
            await firstSessions.GetByIdAsync(session.Id, TestContext.Current.CancellationToken)
        )!;
        Session staleLoaded = (
            await staleSessions.GetByIdAsync(session.Id, TestContext.Current.CancellationToken)
        )!;
        DateTimeOffset completedAt = createdAt.AddMinutes(1);
        DateTimeOffset revokedAt = createdAt.AddMinutes(2);

        await firstSessions.UpdateAsync(
            firstLoaded.Complete(completedAt),
            TestContext.Current.CancellationToken
        );
        await firstAudits.AddAsync(
            AuditEvent.CreateSessionCompleted(session.Id, completedAt, "{}"),
            TestContext.Current.CancellationToken
        );
        await new EfSessionActionUnitOfWork(firstContext).SaveChangesAsync(
            TestContext.Current.CancellationToken
        );

        await staleSessions.UpdateAsync(
            staleLoaded.Revoke(revokedAt),
            TestContext.Current.CancellationToken
        );
        await staleAudits.AddAsync(
            AuditEvent.CreateSessionRevoked(session.Id, revokedAt, "{}"),
            TestContext.Current.CancellationToken
        );
        await Assert.ThrowsAsync<PersistenceConflictException>(() =>
            new EfSessionActionUnitOfWork(staleContext).SaveChangesAsync(
                TestContext.Current.CancellationToken
            )
        );

        await using GatekeeperDbContext verifyContext = new GatekeeperDbContext(options);
        Session? found = await new EfSessionRepository(verifyContext).GetByIdAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );
        int terminalAuditCount = await verifyContext.AuditEvents.CountAsync(
            auditEvent =>
                auditEvent.AggregateId == session.Id
                && (
                    auditEvent.EventType == "SessionCompleted"
                    || auditEvent.EventType == "SessionRevoked"
                    || auditEvent.EventType == "SessionExpired"
                ),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(found);
        Assert.Equal(SessionStatus.Completed, found.Status);
        Assert.Equal(completedAt, found.CompletedAt);
        Assert.Null(found.RevokedAt);
        Assert.Equal(1, terminalAuditCount);
    }

    [Fact]
    public async Task ActionSlotReservationRollsBack_When_AuditSaveFails()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        Session session = Session.Load(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionStatus.Active,
            ["prod-api"],
            ["test.echo"],
            createdAt,
            createdAt.AddMinutes(30),
            actionCount: 0,
            maxActionCount: 10,
            completedAt: null,
            revokedAt: null,
            expiredAt: null
        );
        Guid duplicateAuditId = Guid.NewGuid();

        await using (GatekeeperDbContext seedContext = new GatekeeperDbContext(options))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await new EfSessionRepository(seedContext).AddAsync(
                session,
                TestContext.Current.CancellationToken
            );
            await seedContext.AuditEvents.AddAsync(
                new AuditEventEntity
                {
                    Id = duplicateAuditId,
                    EventType = "AlreadyExists",
                    AggregateId = session.Id,
                    OccurredAt = createdAt,
                    PayloadJson = "{}",
                },
                TestContext.Current.CancellationToken
            );
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (GatekeeperDbContext actionContext = new GatekeeperDbContext(options))
        {
            var audits = new EfAuditEventRepository(actionContext);
            await audits.AddAsync(
                AuditEvent.Load(
                    duplicateAuditId,
                    "DuplicatePendingAudit",
                    session.Id,
                    createdAt.AddSeconds(1),
                    "{}"
                ),
                TestContext.Current.CancellationToken
            );

            var unitOfWork = new EfSessionActionUnitOfWork(actionContext);
            AuditEvent allowedAudit = AuditEvent.CreateSessionActionAllowed(
                session.Id,
                createdAt.AddSeconds(2),
                "{}"
            );

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                unitOfWork.TryReserveActionSlotAndSaveChangesAsync(
                    session.Id,
                    createdAt.AddSeconds(2),
                    allowedAudit,
                    TestContext.Current.CancellationToken
                )
            );
        }

        await using GatekeeperDbContext verifyContext = new GatekeeperDbContext(options);
        int actionCount = await verifyContext
            .Sessions.Where(item => item.Id == session.Id)
            .Select(item => item.ActionCount)
            .SingleAsync(TestContext.Current.CancellationToken);
        bool allowedAuditPersisted = await verifyContext.AuditEvents.AnyAsync(
            auditEvent =>
                auditEvent.AggregateId == session.Id
                && auditEvent.EventType == "SessionActionAllowed",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, actionCount);
        Assert.False(allowedAuditPersisted);
    }

    [Fact]
    public async Task LifecycleUpdatePreservesConcurrentActionCountReservation()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        Session session = Session.Load(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionStatus.Active,
            ["prod-api"],
            ["test.echo"],
            createdAt,
            createdAt.AddMinutes(30),
            actionCount: 0,
            maxActionCount: 10,
            completedAt: null,
            revokedAt: null,
            expiredAt: null
        );

        await using (GatekeeperDbContext seedContext = new GatekeeperDbContext(options))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await new EfSessionRepository(seedContext).AddAsync(
                session,
                TestContext.Current.CancellationToken
            );
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Session staleLoaded;
        await using (GatekeeperDbContext staleContext = new GatekeeperDbContext(options))
        {
            staleLoaded = (
                await new EfSessionRepository(staleContext).GetByIdAsync(
                    session.Id,
                    TestContext.Current.CancellationToken
                )
            )!;
        }

        DateTimeOffset actionAt = createdAt.AddMinutes(1);
        await using (GatekeeperDbContext actionContext = new GatekeeperDbContext(options))
        {
            var unitOfWork = new EfSessionActionUnitOfWork(actionContext);
            bool reserved = await unitOfWork.TryReserveActionSlotAndSaveChangesAsync(
                session.Id,
                actionAt,
                AuditEvent.CreateSessionActionAllowed(session.Id, actionAt, "{}"),
                TestContext.Current.CancellationToken
            );

            Assert.True(reserved);
        }

        DateTimeOffset completedAt = createdAt.AddMinutes(2);
        await using (GatekeeperDbContext lifecycleContext = new GatekeeperDbContext(options))
        {
            var sessions = new EfSessionRepository(lifecycleContext);
            var audits = new EfAuditEventRepository(lifecycleContext);
            await sessions.UpdateAsync(
                staleLoaded.Complete(completedAt),
                TestContext.Current.CancellationToken
            );
            await audits.AddAsync(
                AuditEvent.CreateSessionCompleted(session.Id, completedAt, "{}"),
                TestContext.Current.CancellationToken
            );
            await new EfSessionActionUnitOfWork(lifecycleContext).SaveChangesAsync(
                TestContext.Current.CancellationToken
            );
        }

        await using GatekeeperDbContext verifyContext = new GatekeeperDbContext(options);
        Session? found = await new EfSessionRepository(verifyContext).GetByIdAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );
        bool allowedAuditPersisted = await verifyContext.AuditEvents.AnyAsync(
            auditEvent =>
                auditEvent.AggregateId == session.Id
                && auditEvent.EventType == "SessionActionAllowed",
            TestContext.Current.CancellationToken
        );
        bool completedAuditPersisted = await verifyContext.AuditEvents.AnyAsync(
            auditEvent =>
                auditEvent.AggregateId == session.Id && auditEvent.EventType == "SessionCompleted",
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(found);
        Assert.Equal(SessionStatus.Completed, found.Status);
        Assert.Equal(1, found.ActionCount);
        Assert.Equal(10, found.MaxActionCount);
        Assert.Equal(completedAt, found.CompletedAt);
        Assert.True(allowedAuditPersisted);
        Assert.True(completedAuditPersisted);
    }

    [Fact]
    public async Task ActionSlotReservationRejectsExpiredSessionAtomically()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        Session session = Session.Load(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionStatus.Active,
            ["prod-api"],
            ["test.echo"],
            createdAt,
            createdAt.AddMinutes(1),
            actionCount: 0,
            maxActionCount: 10,
            completedAt: null,
            revokedAt: null,
            expiredAt: null
        );

        await using (GatekeeperDbContext seedContext = new GatekeeperDbContext(options))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await new EfSessionRepository(seedContext).AddAsync(
                session,
                TestContext.Current.CancellationToken
            );
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        DateTimeOffset reservationTime = createdAt.AddMinutes(1).AddTicks(1);
        await using (GatekeeperDbContext actionContext = new GatekeeperDbContext(options))
        {
            var unitOfWork = new EfSessionActionUnitOfWork(actionContext);
            bool reserved = await unitOfWork.TryReserveActionSlotAndSaveChangesAsync(
                session.Id,
                reservationTime,
                AuditEvent.CreateSessionActionAllowed(session.Id, reservationTime, "{}"),
                TestContext.Current.CancellationToken
            );

            Assert.False(reserved);
        }

        await using GatekeeperDbContext verifyContext = new GatekeeperDbContext(options);
        Session? found = await new EfSessionRepository(verifyContext).GetByIdAsync(
            session.Id,
            TestContext.Current.CancellationToken
        );
        bool allowedAuditPersisted = await verifyContext.AuditEvents.AnyAsync(
            auditEvent =>
                auditEvent.AggregateId == session.Id
                && auditEvent.EventType == "SessionActionAllowed",
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(found);
        Assert.Equal(SessionStatus.Active, found.Status);
        Assert.Equal(0, found.ActionCount);
        Assert.False(allowedAuditPersisted);
    }

    [Fact]
    public async Task DuplicateSessionAccessRequestIdIsRejectedByDatabaseConstraint()
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<GatekeeperDbContext> options =
            new DbContextOptionsBuilder<GatekeeperDbContext>().UseSqlite(connection).Options;

        var createdAt = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var request = AccessRequest.Create(
            "Investigate production incident",
            "agent-1",
            ["prod-api"],
            ["read_logs"],
            30,
            RiskLevel.Medium,
            "Need diagnosis",
            ["tail logs"],
            ["restart service"],
            new Dictionary<string, string> { ["ticket"] = "INC-1" },
            createdAt
        );
        AccessRequest approved = request.Approve(createdAt);
        Session firstSession = Session.CreateFromApprovedAccessRequest(approved, createdAt);
        Session secondSession = Session.CreateFromApprovedAccessRequest(
            approved,
            createdAt.AddSeconds(1)
        );

        await using GatekeeperDbContext dbContext = new GatekeeperDbContext(options);
        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new EfSessionRepository(dbContext);
        await repository.AddAsync(firstSession, TestContext.Current.CancellationToken);
        await repository.AddAsync(secondSession, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            dbContext.SaveChangesAsync(TestContext.Current.CancellationToken)
        );
    }

    private sealed class FixedClock : IClock
    {
        private readonly DateTimeOffset _utcNow;

        public FixedClock(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTimeOffset UtcNow => _utcNow;
    }
}
