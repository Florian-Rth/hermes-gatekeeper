using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gatekeeper.Infrastructure;

public static class DependencyInjection
{
    private const string DefaultSqliteDataPath = "/data/gatekeeper.db";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = ResolveConnectionString(configuration);
        EnsureSqliteDirectoryExists(connectionString);

        services.AddDbContext<GatekeeperDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IAccessRequestRepository, EfAccessRequestRepository>();
        services.AddScoped<IAccessRequestUnitOfWork, EfAccessRequestUnitOfWork>();
        services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();
        services.AddScoped<IAccessRequestService, AccessRequestService>();

        return services;
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        string? configuredConnectionString = configuration.GetConnectionString("Gatekeeper");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        string? configuredDataPath = Environment.GetEnvironmentVariable(
            "GATEKEEPER_SQLITE_DATA_PATH"
        );
        string dataPath = string.IsNullOrWhiteSpace(configuredDataPath)
            ? DefaultSqliteDataPath
            : configuredDataPath;

        return new SqliteConnectionStringBuilder { DataSource = dataPath }.ToString();
    }

    private static void EnsureSqliteDirectoryExists(string connectionString)
    {
        SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return;
        }

        string? directory = Path.GetDirectoryName(builder.DataSource);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }
}
