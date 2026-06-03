using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gatekeeper.Tests;

public sealed class ApprovalCatalogIntegrationTests
{
    [Fact]
    public async Task ApproveAsync_RejectsUnknownCatalogTarget()
    {
        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);

        await using GatekeeperApiFactory factory = CreateFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAccessRequestService service =
            scope.ServiceProvider.GetRequiredService<IAccessRequestService>();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();

        AccessRequestDetails created = await service.CreateAsync(
            new CreateAccessRequestCommand(
                "Inspect unknown ssh target",
                "agent-1",
                ["missing-ssh"],
                ["remote.readonly.inspect"],
                15,
                RiskLevel.Low,
                "integration test",
                ["inspect status"],
                [],
                new Dictionary<string, string>()
            ),
            TestContext.Current.CancellationToken
        );

        ApprovalResult result = await service.ApproveAsync(
            new ApproveAccessRequestCommand(created.Id, "approve"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Conflict);
        Assert.Null(result.Session);
        Assert.Equal(0, await dbContext.Sessions.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApproveAsync_RejectsUnknownCatalogProfileForKnownTarget()
    {
        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);

        await using GatekeeperApiFactory factory = CreateFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAccessRequestService service =
            scope.ServiceProvider.GetRequiredService<IAccessRequestService>();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();

        AccessRequestDetails created = await service.CreateAsync(
            new CreateAccessRequestCommand(
                "Restart demo app",
                "agent-1",
                ["demo-ssh"],
                ["remote.unknown.profile"],
                15,
                RiskLevel.High,
                "integration test",
                ["restart demo app"],
                [],
                new Dictionary<string, string>()
            ),
            TestContext.Current.CancellationToken
        );

        ApprovalResult result = await service.ApproveAsync(
            new ApproveAccessRequestCommand(created.Id, "approve"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Conflict);
        Assert.Null(result.Session);
        Assert.Equal(0, await dbContext.Sessions.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApproveAsync_CreatesSessionForCatalogBackedProfile()
    {
        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);

        await using GatekeeperApiFactory factory = CreateFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAccessRequestService service =
            scope.ServiceProvider.GetRequiredService<IAccessRequestService>();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        ISessionRepository sessionRepository =
            scope.ServiceProvider.GetRequiredService<ISessionRepository>();

        AccessRequestDetails created = await service.CreateAsync(
            new CreateAccessRequestCommand(
                "Inspect demo ssh target",
                "agent-1",
                ["demo-ssh"],
                ["remote.readonly.inspect"],
                15,
                RiskLevel.Low,
                "integration test",
                ["check status"],
                [],
                new Dictionary<string, string>()
            ),
            TestContext.Current.CancellationToken
        );

        ApprovalResult result = await service.ApproveAsync(
            new ApproveAccessRequestCommand(created.Id, "approve"),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Conflict);
        Assert.NotNull(result.Session);
        Session persistedSession =
            await sessionRepository.GetByIdAsync(
                result.Session!.Id,
                TestContext.Current.CancellationToken
            ) ?? throw new Xunit.Sdk.XunitException("Expected persisted session.");
        SshProfileGrant grant = Assert.Single(persistedSession.SshProfileGrants);
        Assert.Equal("demo-ssh", grant.TargetAlias);
        Assert.Equal("remote.readonly.inspect", grant.ProfileName);
    }

    private static GatekeeperApiFactory CreateFactory(string connectionString)
    {
        return new GatekeeperApiFactory(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ConnectionStrings__Gatekeeper"] = connectionString,
                ["GATEKEEPER_ADMIN_TOKEN"] = "test-admin-token",
                ["GATEKEEPER_ADMIN_USERNAME"] = "admin",
                ["GATEKEEPER_ADMIN_PASSWORD"] = "correct-password",
                ["GATEKEEPER_ADMIN_COOKIE_SECURE"] = "false",
            }
        );
    }
}
