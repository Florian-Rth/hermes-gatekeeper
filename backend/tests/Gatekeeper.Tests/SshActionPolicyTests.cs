using System.Reflection;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure;
using Gatekeeper.Infrastructure.SessionActions.Ssh;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gatekeeper.Tests;

public sealed class SshActionPolicyTests
{
    [Fact]
    public void AddInfrastructure_Should_LoadSshConnectorOptionsFromConfiguration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Gatekeeper"] = "Data Source=:memory:",
                    ["SshConnector:Targets:demo-ssh:Host"] = "demo-ssh",
                    ["SshConnector:Targets:demo-ssh:Port"] = "2222",
                    ["SshConnector:Targets:demo-ssh:Username"] = "gatekeeper-readonly",
                    ["SshConnector:Targets:demo-ssh:Profiles:remote.readonly.inspect:Actions:0"] =
                        "system.status.read",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:Command:0"] =
                        "uname",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:Command:1"] = "-a",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:IsMutating"] =
                        "false",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:Risk"] = "Low",
                }
            )
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        SshConnectorOptions options = provider.GetRequiredService<SshConnectorOptions>();
        Assert.True(options.Targets.ContainsKey("demo-ssh"));
        Assert.Equal("demo-ssh", options.Targets["demo-ssh"].Host);
        Assert.Equal(2222, options.Targets["demo-ssh"].Port);
        Assert.Equal(
            new[] { "system.status.read" },
            options.Targets["demo-ssh"].Profiles["remote.readonly.inspect"].Actions
        );
        Assert.Equal(
            new[] { "uname", "-a" },
            options.Targets["demo-ssh"].Actions["system.status.read"].Command
        );
        Assert.False(options.Targets["demo-ssh"].Actions["system.status.read"].IsMutating);
        Assert.Equal(RiskLevel.Low, options.Targets["demo-ssh"].Actions["system.status.read"].Risk);
        Assert.IsType<DbSshActionPolicy>(provider.GetRequiredService<ISshActionPolicy>());
    }

    [Fact]
    public void AddInfrastructure_Should_FailFast_When_PostgreSqlProviderHasNoConnectionString()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Gatekeeper:DatabaseProvider"] = "PostgreSql" }
            )
            .Build();
        var services = new ServiceCollection();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration)
        );

        Assert.Contains(
            "ConnectionStrings:Gatekeeper",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddInfrastructure_Should_FailFast_When_DatabaseProviderValueIsUnsupported()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Gatekeeper:DatabaseProvider"] = "Postgres",
                    ["ConnectionStrings:Gatekeeper"] =
                        "Host=localhost;Port=5432;Database=gatekeeper;Username=test;Password=test",
                }
            )
            .Build();
        var services = new ServiceCollection();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration)
        );

        Assert.Contains(
            "Unsupported Gatekeeper database provider",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddInfrastructure_Should_SanitizeInvalidSshConnectorNumericOptions()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Gatekeeper"] = "Data Source=:memory:",
                    ["SshConnector:Targets:demo-ssh:Host"] = "demo-ssh",
                    ["SshConnector:Targets:demo-ssh:Port"] = "70000",
                    ["SshConnector:Targets:demo-ssh:DefaultTimeoutSeconds"] = "0",
                    ["SshConnector:Targets:demo-ssh:DefaultOutputLimitBytes"] = "-1",
                    ["SshConnector:Targets:demo-ssh:Profiles:remote.readonly.inspect:Actions:0"] =
                        "system.status.read",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:Command:0"] =
                        "uname",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:IsMutating"] =
                        "false",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:Risk"] = "Low",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:TimeoutSeconds"] =
                        "0",
                    ["SshConnector:Targets:demo-ssh:Actions:system.status.read:OutputLimitBytes"] =
                        "-1",
                }
            )
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        SshTargetOptions target = provider.GetRequiredService<SshConnectorOptions>().Targets[
            "demo-ssh"
        ];
        Assert.Equal(22, target.Port);
        Assert.Equal(10, target.DefaultTimeoutSeconds);
        Assert.Equal(8192, target.DefaultOutputLimitBytes);
        Assert.Null(target.Actions["system.status.read"].TimeoutSeconds);
        Assert.Null(target.Actions["system.status.read"].OutputLimitBytes);
    }

    [Fact]
    public void PublicExecutionContracts_Should_NotExposeCommandStringInput()
    {
        PropertyInfo[] commandProperties = typeof(ExecuteSessionActionCommand).GetProperties();
        MethodInfo resolveMethod = typeof(ISshActionPolicy).GetMethod(
            nameof(ISshActionPolicy.Resolve)
        )!;
        ParameterInfo[] resolveParameters = resolveMethod.GetParameters();

        Assert.DoesNotContain(commandProperties, property => IsCommandInputName(property.Name));
        Assert.DoesNotContain(resolveParameters, parameter => IsCommandInputName(parameter.Name!));
    }

    private static bool IsCommandInputName(string name)
    {
        return string.Equals(name, "Command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "CommandText", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "CommandString", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "ShellCommand", StringComparison.OrdinalIgnoreCase);
    }
}
