using Testcontainers.PostgreSql;

namespace Gatekeeper.Tests.TestInfrastructure;

internal sealed class PostgresGatekeeperDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("gatekeeper")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _container.StartAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _container.DisposeAsync();
    }
}
