using System.Reflection;
using System.Text.Json;
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
        Assert.IsType<ConfiguredSshActionPolicy>(provider.GetRequiredService<ISshActionPolicy>());
    }

    [Fact]
    public void Resolve_Should_AuthorizeAction_When_TargetProfileAndActionMatch()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "service.status.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            JsonSerializer.SerializeToElement(new { service = "sshd" })
        );

        Assert.True(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.None, result.FailureReason);
        Assert.NotNull(result.ResolvedAction);
        Assert.Equal("demo-ssh", result.ResolvedAction.TargetAlias);
        Assert.Equal("service.status.read", result.ResolvedAction.ActionName);
        Assert.Equal(new[] { "systemctl", "is-active", "sshd" }, result.ResolvedAction.Command);
        Assert.Equal("sshd", result.ResolvedAction.SafeParameters["service"]);
        Assert.Equal(TimeSpan.FromSeconds(7), result.ResolvedAction.Timeout);
        Assert.Equal(4096, result.ResolvedAction.OutputLimitBytes);
        Assert.False(result.ResolvedAction.IsMutating);
        Assert.Equal(RiskLevel.Low, result.ResolvedAction.Risk);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_TargetIsUnknown()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "unknown-ssh",
            "system.status.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            null
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.UnknownTarget, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_ActionIsUnknown()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "unknown.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            null
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.UnknownAction, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_NoApprovedProfilePermitsAction()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "system.status.read",
            Grants(("demo-ssh", "remote.readonly.narrow")),
            null
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.MissingProfileMembership, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_ParameterValueIsUnsupported()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "service.status.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            JsonSerializer.SerializeToElement(new { service = "postgresql" })
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.InvalidParameter, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_NotAuthorizeGrantForDifferentTarget_When_ProfileNameMatches()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "system.status.read",
            Grants(("other-ssh", "remote.readonly.inspect")),
            null
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.MissingProfileMembership, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_NotAuthorizeMutatingAction_ForReadonlyProfile()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "service.restart",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            JsonSerializer.SerializeToElement(new { service = "demo-app" })
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.MissingProfileMembership, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_AuthorizeOnlyConfiguredMutatingActions_ForMaintenanceProfile()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult allowedResult = policy.Resolve(
            "demo-ssh",
            "service.restart",
            Grants(("demo-ssh", "remote.maintenance.basic")),
            JsonSerializer.SerializeToElement(new { service = "demo-app" })
        );

        Assert.True(allowedResult.Succeeded);
        Assert.NotNull(allowedResult.ResolvedAction);
        Assert.True(allowedResult.ResolvedAction.IsMutating);
        Assert.Equal(RiskLevel.High, allowedResult.ResolvedAction.Risk);

        SshActionPolicyResult deniedResult = policy.Resolve(
            "demo-ssh",
            "disk.usage.read",
            Grants(("demo-ssh", "remote.maintenance.basic")),
            null
        );

        Assert.False(deniedResult.Succeeded);
        Assert.Equal(
            SshActionPolicyFailureReason.MissingProfileMembership,
            deniedResult.FailureReason
        );
        Assert.Null(deniedResult.ResolvedAction);
        Assert.NotNull(deniedResult.Error);
    }

    [Fact]
    public void Resolve_Should_NotAuthorizeReload_ForReadonlyProfile()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "service.reload",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            JsonSerializer.SerializeToElement(new { service = "demo-app" })
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.MissingProfileMembership, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_AuthorizeReload_ForMaintenanceProfile_WithDistinctActionNameAndCommand()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "service.reload",
            Grants(("demo-ssh", "remote.maintenance.basic")),
            JsonSerializer.SerializeToElement(new { service = "demo-app" })
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ResolvedAction);
        Assert.Equal("service.reload", result.ResolvedAction.ActionName);
        Assert.NotEqual("service.restart", result.ResolvedAction.ActionName);
        Assert.Equal(new[] { "systemctl", "reload", "demo-app" }, result.ResolvedAction.Command);
        Assert.True(result.ResolvedAction.IsMutating);
        Assert.Equal(RiskLevel.High, result.ResolvedAction.Risk);
    }

    [Fact]
    public void Resolve_Should_NotAuthorizeBackupTrigger_ForReadonlyProfile()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "backup.trigger",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            JsonSerializer.SerializeToElement(new { job = "nightly-config" })
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.MissingProfileMembership, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_AuthorizeBackupTrigger_ForMaintenanceProfile_WithDistinctActionAndJobSemantics()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "backup.trigger",
            Grants(("demo-ssh", "remote.maintenance.basic")),
            JsonSerializer.SerializeToElement(new { job = "nightly-config" })
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ResolvedAction);
        Assert.Equal("backup.trigger", result.ResolvedAction.ActionName);
        Assert.NotEqual("service.reload", result.ResolvedAction.ActionName);
        Assert.NotEqual("service.restart", result.ResolvedAction.ActionName);
        Assert.Equal(
            new[] { "backup-job", "trigger", "nightly-config" },
            result.ResolvedAction.Command
        );
        Assert.Equal("nightly-config", result.ResolvedAction.SafeParameters["job"]);
        Assert.False(result.ResolvedAction.SafeParameters.ContainsKey("service"));
        Assert.True(result.ResolvedAction.IsMutating);
        Assert.Equal(RiskLevel.High, result.ResolvedAction.Risk);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_ParameterIsDuplicated()
    {
        ConfiguredSshActionPolicy policy = CreatePolicy();
        using JsonDocument document = JsonDocument.Parse(
            "{\"service\":\"ssh\",\"service\":\"ssh\"}"
        );
        JsonElement parameters = document.RootElement.Clone();

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "service.status.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            parameters
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.InvalidParameter, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_CommandIsNotConfigured()
    {
        SshConnectorOptions options = CreateOptions();
        options.Targets["demo-ssh"].Profiles["remote.readonly.inspect"].Actions.Add("empty.read");
        options.Targets["demo-ssh"].Actions["empty.read"] = new SshActionOptions
        {
            IsMutating = false,
            Risk = RiskLevel.Low,
        };
        ConfiguredSshActionPolicy policy = new ConfiguredSshActionPolicy(options);

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "empty.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            null
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.InvalidConfiguration, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_ActionMetadataIsMissing()
    {
        SshConnectorOptions options = CreateOptions();
        options.Targets["demo-ssh"].Actions["system.status.read"].Risk = null;
        ConfiguredSshActionPolicy policy = new ConfiguredSshActionPolicy(options);

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "system.status.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            null
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.InvalidConfiguration, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_ActionMetadataCombinationIsInvalid()
    {
        SshConnectorOptions options = CreateOptions();
        SshActionOptions action = options.Targets["demo-ssh"].Actions["service.restart"];
        action.IsMutating = true;
        action.Risk = RiskLevel.Low;
        ConfiguredSshActionPolicy policy = new ConfiguredSshActionPolicy(options);

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "service.restart",
            Grants(("demo-ssh", "remote.maintenance.basic")),
            JsonSerializer.SerializeToElement(new { service = "demo-app" })
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.InvalidConfiguration, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_FailWithTypedFailure_When_CommandTemplatePlaceholderIsUnresolved()
    {
        SshConnectorOptions options = CreateOptions();
        options.Targets["demo-ssh"].Actions["system.status.read"].CommandTemplate = new List<string>
        {
            "systemctl",
            "status",
            "{service}",
        };
        ConfiguredSshActionPolicy policy = new ConfiguredSshActionPolicy(options);

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "system.status.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            null
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshActionPolicyFailureReason.InvalidConfiguration, result.FailureReason);
        Assert.Null(result.ResolvedAction);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Resolve_Should_UseTargetDefaults_When_ActionNumericValuesAreInvalid()
    {
        SshConnectorOptions options = CreateOptions();
        SshActionOptions action = options.Targets["demo-ssh"].Actions["system.status.read"];
        action.TimeoutSeconds = 0;
        action.OutputLimitBytes = -1;
        ConfiguredSshActionPolicy policy = new ConfiguredSshActionPolicy(options);

        SshActionPolicyResult result = policy.Resolve(
            "demo-ssh",
            "system.status.read",
            Grants(("demo-ssh", "remote.readonly.inspect")),
            null
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ResolvedAction);
        Assert.Equal(TimeSpan.FromSeconds(7), result.ResolvedAction.Timeout);
        Assert.Equal(4096, result.ResolvedAction.OutputLimitBytes);
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

    private static IReadOnlyCollection<SshApprovedProfileGrant> Grants(
        params (string TargetAlias, string ProfileName)[] grants
    )
    {
        return grants
            .Select(grant => new SshApprovedProfileGrant(grant.TargetAlias, grant.ProfileName))
            .ToArray();
    }

    private static ConfiguredSshActionPolicy CreatePolicy()
    {
        return new ConfiguredSshActionPolicy(CreateOptions());
    }

    private static SshConnectorOptions CreateOptions()
    {
        return new SshConnectorOptions
        {
            Targets = new Dictionary<string, SshTargetOptions>(StringComparer.Ordinal)
            {
                ["demo-ssh"] = new SshTargetOptions
                {
                    Host = "demo-ssh",
                    Port = 22,
                    Username = "gatekeeper-readonly",
                    PrivateKeyPath = "/run/secrets/demo-key",
                    KnownHostsPath = "/app/config/known_hosts",
                    DefaultTimeoutSeconds = 7,
                    DefaultOutputLimitBytes = 4096,
                    Profiles = new Dictionary<string, SshProfileOptions>(StringComparer.Ordinal)
                    {
                        ["remote.readonly.inspect"] = new SshProfileOptions
                        {
                            Actions = new List<string>
                            {
                                "system.status.read",
                                "service.status.read",
                            },
                        },
                        ["remote.readonly.narrow"] = new SshProfileOptions
                        {
                            Actions = new List<string> { "disk.usage.read" },
                        },
                        ["remote.maintenance.basic"] = new SshProfileOptions
                        {
                            Actions = new List<string>
                            {
                                "service.restart",
                                "service.reload",
                                "backup.trigger",
                            },
                        },
                    },
                    Actions = new Dictionary<string, SshActionOptions>(StringComparer.Ordinal)
                    {
                        ["system.status.read"] = new SshActionOptions
                        {
                            Command = new List<string> { "uname", "-a" },
                            IsMutating = false,
                            Risk = RiskLevel.Low,
                        },
                        ["disk.usage.read"] = new SshActionOptions
                        {
                            Command = new List<string> { "df", "-h" },
                            IsMutating = false,
                            Risk = RiskLevel.Low,
                        },
                        ["service.status.read"] = new SshActionOptions
                        {
                            CommandTemplate = new List<string>
                            {
                                "systemctl",
                                "is-active",
                                "{service}",
                            },
                            AllowedParameters = new Dictionary<string, List<string>>(
                                StringComparer.Ordinal
                            )
                            {
                                ["service"] = new List<string> { "sshd" },
                            },
                            IsMutating = false,
                            Risk = RiskLevel.Low,
                        },
                        ["service.restart"] = new SshActionOptions
                        {
                            CommandTemplate = new List<string>
                            {
                                "systemctl",
                                "restart",
                                "{service}",
                            },
                            AllowedParameters = new Dictionary<string, List<string>>(
                                StringComparer.Ordinal
                            )
                            {
                                ["service"] = new List<string> { "demo-app" },
                            },
                            IsMutating = true,
                            Risk = RiskLevel.High,
                        },
                        ["service.reload"] = new SshActionOptions
                        {
                            CommandTemplate = new List<string>
                            {
                                "systemctl",
                                "reload",
                                "{service}",
                            },
                            AllowedParameters = new Dictionary<string, List<string>>(
                                StringComparer.Ordinal
                            )
                            {
                                ["service"] = new List<string> { "demo-app" },
                            },
                            IsMutating = true,
                            Risk = RiskLevel.High,
                        },
                        ["backup.trigger"] = new SshActionOptions
                        {
                            CommandTemplate = new List<string> { "backup-job", "trigger", "{job}" },
                            AllowedParameters = new Dictionary<string, List<string>>(
                                StringComparer.Ordinal
                            )
                            {
                                ["job"] = new List<string> { "nightly-config" },
                            },
                            IsMutating = true,
                            Risk = RiskLevel.High,
                        },
                    },
                },
            },
        };
    }
}
