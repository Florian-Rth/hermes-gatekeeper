using Gatekeeper.Tests.TestInfrastructure;

namespace Gatekeeper.Tests;

public sealed class PostgresMigrationIntegrationTests
{
    [Fact]
    public async Task AppStartsAndMigrateAsyncSucceedsAgainstPostgresContainer()
    {
        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);

        using GatekeeperApiFactory factory = new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__Gatekeeper"] = database.ConnectionString,
                ["GATEKEEPER_ADMIN_TOKEN"] = "test-admin-token",
                ["GATEKEEPER_ADMIN_USERNAME"] = "admin",
                ["GATEKEEPER_ADMIN_PASSWORD"] = "correct-password",
                ["GATEKEEPER_ADMIN_COOKIE_SECURE"] = "false",
            }
        );

        using HttpClient client = factory.CreateClient();

        await factory.MigrateAsync(TestContext.Current.CancellationToken);
    }
}
