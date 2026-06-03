using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Gatekeeper.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gatekeeper.Tests;

public sealed class CatalogSeedIntegrationTests
{
    [Fact]
    public async Task InitialStartupSeedsExpectedCatalogFromDevelopmentConfiguration()
    {
        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);

        using GatekeeperApiFactory factory = CreateFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();

        SshTargetEntity target = await dbContext.SshTargets.SingleAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal("demo-ssh", target.Alias);
        Assert.Equal("demo-ssh", target.Host);
        Assert.Equal(22, target.Port);
        Assert.Equal("gatekeeper-readonly", target.Username);
        Assert.Equal(
            2,
            await dbContext.SshProfiles.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            6,
            await dbContext.SshActions.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            6,
            await dbContext.SshProfileActions.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            4,
            await dbContext.SshActionAllowedParameters.CountAsync(
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(
            4,
            await dbContext.SshActionAllowedParameterValues.CountAsync(
                TestContext.Current.CancellationToken
            )
        );

        SshActionEntity serviceStatus = await dbContext
            .SshActions.Include(action => action.AllowedParameters)
                .ThenInclude(parameter => parameter.AllowedValues)
            .SingleAsync(
                action => action.Name == "service.status.read",
                TestContext.Current.CancellationToken
            );

        SshActionAllowedParameterEntity serviceParameter = Assert.Single(
            serviceStatus.AllowedParameters
        );
        Assert.Equal("service", serviceParameter.Name);
        Assert.Equal(
            new[] { "sshd" },
            serviceParameter.AllowedValues.Select(value => value.Value).ToArray()
        );
    }

    [Fact]
    public async Task InitialStartupPersistsMutatingAndRiskMetadata()
    {
        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);

        using GatekeeperApiFactory factory = CreateFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();

        SshActionEntity readAction = await dbContext.SshActions.SingleAsync(
            action => action.Name == "system.status.read",
            TestContext.Current.CancellationToken
        );
        SshActionEntity mutatingAction = await dbContext.SshActions.SingleAsync(
            action => action.Name == "service.restart",
            TestContext.Current.CancellationToken
        );

        Assert.False(readAction.IsMutating);
        Assert.Equal(Gatekeeper.Core.AccessRequests.RiskLevel.Low, readAction.Risk);
        Assert.True(mutatingAction.IsMutating);
        Assert.Equal(Gatekeeper.Core.AccessRequests.RiskLevel.High, mutatingAction.Risk);
        Assert.Equal(15, mutatingAction.TimeoutSeconds);
        Assert.Equal(4096, mutatingAction.OutputLimitBytes);
    }

    [Fact]
    public async Task SecondStartupDoesNotOverwriteExistingCatalogEntries()
    {
        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);

        await using (GatekeeperApiFactory firstFactory = CreateFactory(database.ConnectionString))
        {
            using HttpClient client = firstFactory.CreateClient();

            await using AsyncServiceScope scope = firstFactory.Services.CreateAsyncScope();
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            SshTargetEntity target = await dbContext.SshTargets.SingleAsync(
                TestContext.Current.CancellationToken
            );
            target.Host = "custom-host.example.internal";
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (GatekeeperApiFactory secondFactory = CreateFactory(database.ConnectionString))
        {
            using HttpClient client = secondFactory.CreateClient();

            await using AsyncServiceScope scope = secondFactory.Services.CreateAsyncScope();
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            SshTargetEntity target = await dbContext.SshTargets.SingleAsync(
                TestContext.Current.CancellationToken
            );
            Assert.Equal("custom-host.example.internal", target.Host);
        }
    }

    [Fact]
    public async Task InvalidSeedDataFailsClearlyDuringStartup()
    {
        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);

        using GatekeeperApiFactory factory = CreateFactory(
            database.ConnectionString,
            new Dictionary<string, string?>
            {
                ["SshConnector__Targets__demo-ssh__Actions__service.restart__Risk"] = "Low",
            }
        );

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                Task.Run(() => factory.CreateClient(), TestContext.Current.CancellationToken)
        );

        Assert.Contains("service.restart", exception.Message, StringComparison.Ordinal);
        Assert.Contains("low risk", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GatekeeperApiFactory CreateFactory(
        string connectionString,
        IReadOnlyDictionary<string, string?>? additionalOverrides = null
    )
    {
        Dictionary<string, string?> overrides = new(StringComparer.Ordinal)
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ConnectionStrings__Gatekeeper"] = connectionString,
            ["GATEKEEPER_ADMIN_TOKEN"] = "test-admin-token",
            ["GATEKEEPER_ADMIN_USERNAME"] = "admin",
            ["GATEKEEPER_ADMIN_PASSWORD"] = "correct-password",
            ["GATEKEEPER_ADMIN_COOKIE_SECURE"] = "false",
        };

        if (additionalOverrides is not null)
        {
            foreach ((string key, string? value) in additionalOverrides)
            {
                overrides[key] = value;
            }
        }

        return new GatekeeperApiFactory(overrides);
    }
}
