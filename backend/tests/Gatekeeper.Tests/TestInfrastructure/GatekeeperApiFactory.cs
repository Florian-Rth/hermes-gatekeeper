using Gatekeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gatekeeper.Tests.TestInfrastructure;

internal sealed class GatekeeperApiFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _environmentOverrides;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

    public GatekeeperApiFactory(
        IReadOnlyDictionary<string, string?> environmentOverrides,
        Action<IServiceCollection>? configureServices = null
    )
    {
        _environmentOverrides = environmentOverrides;
        _configureServices = configureServices;
        ApplyEnvironmentOverrides();
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_configureServices is null)
        {
            return;
        }

        builder.ConfigureServices(_configureServices);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach ((string key, string? originalValue) in _originalValues)
        {
            Environment.SetEnvironmentVariable(key, originalValue);
        }
    }

    private void ApplyEnvironmentOverrides()
    {
        foreach ((string key, string? value) in _environmentOverrides)
        {
            _originalValues[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
