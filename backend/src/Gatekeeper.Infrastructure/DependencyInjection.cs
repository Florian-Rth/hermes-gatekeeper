using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.AuditEvents;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Catalog;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Repositories;
using Gatekeeper.Infrastructure.SessionActions;
using Gatekeeper.Infrastructure.SessionActions.Ssh;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Gatekeeper.Infrastructure;

public static class DependencyInjection
{
    private const string DefaultSqliteDataPath = "/data/gatekeeper.db";
    private const string DatabaseProviderConfigurationKey = "Gatekeeper:DatabaseProvider";
    private const string SessionMaxActionCountVariable = "GATEKEEPER_SESSION_MAX_ACTION_COUNT";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = ResolveConnectionString(configuration);
        GatekeeperDatabaseProvider databaseProvider = ResolveDatabaseProvider(
            configuration,
            connectionString
        );

        ConfigureDbContext(services, connectionString, databaseProvider);
        services.AddScoped<IAccessRequestRepository, EfAccessRequestRepository>();
        services.AddScoped<ISessionRepository, EfSessionRepository>();
        services.AddScoped<IAccessRequestUnitOfWork, EfAccessRequestUnitOfWork>();
        services.AddScoped<ISessionActionUnitOfWork, EfSessionActionUnitOfWork>();
        services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();
        services.AddScoped<IAuditEventQueryRepository, EfAuditEventRepository>();
        services.AddScoped<IAccessRequestService, AccessRequestService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ISessionActionService, SessionActionService>();
        services.AddScoped<ISessionActionAdapter, DummySessionActionAdapter>();
        services.AddScoped<ISshApprovalCatalogValidator, DbSshApprovalCatalogValidator>();
        services.AddScoped<SshCatalogBootstrapSeeder>();
        services.AddSingleton(BuildSshConnectorOptions(configuration));
        services.AddScoped<ISshActionPolicy, DbSshActionPolicy>();
        services.AddSingleton<ISshCommandClient, SshNetCommandClient>();
        services.AddSingleton<ISshCommandExecutor, ConfiguredSshCommandExecutor>();
        services.AddSingleton(
            SessionLifecycleOptions.FromConfiguredValue(
                configuration[SessionMaxActionCountVariable]
                    ?? Environment.GetEnvironmentVariable(SessionMaxActionCountVariable)
            )
        );

        return services;
    }

    private static void ConfigureDbContext(
        IServiceCollection services,
        string connectionString,
        GatekeeperDatabaseProvider databaseProvider
    )
    {
        switch (databaseProvider)
        {
            case GatekeeperDatabaseProvider.PostgreSql:
                services.AddDbContext<GatekeeperDbContext>(options =>
                    options
                        .UseNpgsql(connectionString)
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(RelationalEventId.PendingModelChangesWarning)
                        )
                );
                break;
            case GatekeeperDatabaseProvider.Sqlite:
            default:
                EnsureSqliteDirectoryExists(connectionString);
                services.AddDbContext<GatekeeperDbContext>(options =>
                    options.UseSqlite(connectionString)
                );
                break;
        }
    }

    private static SshConnectorOptions BuildSshConnectorOptions(IConfiguration configuration)
    {
        var options = new SshConnectorOptions();
        IConfigurationSection targetsSection = configuration.GetSection(
            $"{SshConnectorOptions.SectionName}:Targets"
        );

        foreach (IConfigurationSection targetSection in targetsSection.GetChildren())
        {
            var target = new SshTargetOptions
            {
                Host = targetSection["Host"] ?? string.Empty,
                Port = ReadIntInRange(targetSection, "Port", 22, 1, 65535),
                Username = targetSection["Username"] ?? string.Empty,
                PrivateKeyPath = targetSection["PrivateKeyPath"] ?? string.Empty,
                KnownHostsPath = targetSection["KnownHostsPath"] ?? string.Empty,
                DefaultTimeoutSeconds = ReadPositiveInt(targetSection, "DefaultTimeoutSeconds", 10),
                DefaultOutputLimitBytes = ReadPositiveInt(
                    targetSection,
                    "DefaultOutputLimitBytes",
                    8192
                ),
            };

            foreach (
                IConfigurationSection profileSection in targetSection
                    .GetSection("Profiles")
                    .GetChildren()
            )
            {
                target.Profiles[profileSection.Key] = new SshProfileOptions
                {
                    Actions = profileSection
                        .GetSection("Actions")
                        .GetChildren()
                        .Select(a => a.Value ?? string.Empty)
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .ToList(),
                };
            }

            foreach (
                IConfigurationSection actionSection in targetSection
                    .GetSection("Actions")
                    .GetChildren()
            )
            {
                var action = new SshActionOptions
                {
                    Command = actionSection
                        .GetSection("Command")
                        .GetChildren()
                        .Select(a => a.Value ?? string.Empty)
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .ToList(),
                    CommandTemplate = actionSection
                        .GetSection("CommandTemplate")
                        .GetChildren()
                        .Select(a => a.Value ?? string.Empty)
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .ToList(),
                    IsMutating = ReadNullableBool(actionSection, "IsMutating"),
                    Risk = ReadNullableRiskLevel(actionSection, "Risk"),
                    TimeoutSeconds = ReadPositiveNullableInt(actionSection, "TimeoutSeconds"),
                    OutputLimitBytes = ReadPositiveNullableInt(actionSection, "OutputLimitBytes"),
                };

                foreach (
                    IConfigurationSection parameterSection in actionSection
                        .GetSection("AllowedParameters")
                        .GetChildren()
                )
                {
                    action.AllowedParameters[parameterSection.Key] = parameterSection
                        .GetChildren()
                        .Select(v => v.Value ?? string.Empty)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();
                }

                target.Actions[actionSection.Key] = action;
            }

            options.Targets[targetSection.Key] = target;
        }

        return options;
    }

    private static int ReadIntInRange(
        IConfigurationSection section,
        string key,
        int defaultValue,
        int minimumValue,
        int maximumValue
    )
    {
        if (!int.TryParse(section[key], out int value))
        {
            return defaultValue;
        }

        return value >= minimumValue && value <= maximumValue ? value : defaultValue;
    }

    private static int ReadPositiveInt(IConfigurationSection section, string key, int defaultValue)
    {
        if (!int.TryParse(section[key], out int value))
        {
            return defaultValue;
        }

        return value > 0 ? value : defaultValue;
    }

    private static int? ReadPositiveNullableInt(IConfigurationSection section, string key)
    {
        if (!int.TryParse(section[key], out int value))
        {
            return null;
        }

        return value > 0 ? value : null;
    }

    private static bool? ReadNullableBool(IConfigurationSection section, string key)
    {
        if (!bool.TryParse(section[key], out bool value))
        {
            return null;
        }

        return value;
    }

    private static RiskLevel? ReadNullableRiskLevel(IConfigurationSection section, string key)
    {
        if (!Enum.TryParse(section[key], true, out RiskLevel value) || !Enum.IsDefined(value))
        {
            return null;
        }

        return value;
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        if (
            Enum.TryParse(
                configuration[DatabaseProviderConfigurationKey],
                ignoreCase: true,
                out GatekeeperDatabaseProvider configuredProvider
            )
            && configuredProvider == GatekeeperDatabaseProvider.PostgreSql
        )
        {
            string? configuredPostgreSqlConnectionString = configuration.GetConnectionString(
                "Gatekeeper"
            );
            if (string.IsNullOrWhiteSpace(configuredPostgreSqlConnectionString))
            {
                throw new InvalidOperationException(
                    "Gatekeeper database provider is configured as PostgreSql, but ConnectionStrings:Gatekeeper is missing."
                );
            }

            return configuredPostgreSqlConnectionString;
        }

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

    private static GatekeeperDatabaseProvider ResolveDatabaseProvider(
        IConfiguration configuration,
        string connectionString
    )
    {
        string? configuredProviderText = configuration[DatabaseProviderConfigurationKey];
        if (
            !string.IsNullOrWhiteSpace(configuredProviderText)
            && !Enum.TryParse(
                configuredProviderText,
                ignoreCase: true,
                out GatekeeperDatabaseProvider _
            )
        )
        {
            throw new InvalidOperationException(
                $"Unsupported Gatekeeper database provider '{configuredProviderText}'."
            );
        }

        if (
            Enum.TryParse(
                configuredProviderText,
                ignoreCase: true,
                out GatekeeperDatabaseProvider configuredProvider
            )
        )
        {
            return configuredProvider;
        }

        return LooksLikePostgreSqlConnectionString(connectionString)
            ? GatekeeperDatabaseProvider.PostgreSql
            : GatekeeperDatabaseProvider.Sqlite;
    }

    private static bool LooksLikePostgreSqlConnectionString(string connectionString)
    {
        try
        {
            _ = new NpgsqlConnectionStringBuilder(connectionString);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
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

    private enum GatekeeperDatabaseProvider
    {
        Sqlite,
        PostgreSql,
    }
}
