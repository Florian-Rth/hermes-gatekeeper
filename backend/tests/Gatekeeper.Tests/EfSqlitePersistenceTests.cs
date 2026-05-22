using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence;
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
