using System.Text.Json;
using Gatekeeper.Api.AgentAuthentication;
using Microsoft.Extensions.Configuration;

namespace Gatekeeper.Tests;

public sealed class AgentAuthOptionsTests
{
    [Fact]
    public void Should_BindSuccessfully_When_ConfigHasOneAgentKey()
    {
        AgentAuthOptions options = CreateOptions(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        Assert.True(options.Enabled);
        Assert.True(options.IsConfigured());
        AgentApiKeyOptions apiKey = Assert.Single(options.ApiKeys);
        Assert.Equal("agent-one", apiKey.AgentId);
        Assert.Equal("secret-one", apiKey.Key);
    }

    [Fact]
    public void Should_BindAndPreserveDistinctIds_When_ConfigHasMultipleAgentKeys()
    {
        AgentAuthOptions options = CreateOptions(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
                ["AgentAuthentication:ApiKeys:1:AgentId"] = "agent-two",
                ["AgentAuthentication:ApiKeys:1:Key"] = "secret-two",
            }
        );

        Assert.Equal(2, options.ApiKeys.Count);
        Assert.Equal(
            new[] { "agent-one", "agent-two" },
            options.ApiKeys.Select(apiKey => apiKey.AgentId)
        );
        Assert.Equal(
            new[] { "secret-one", "secret-two" },
            options.ApiKeys.Select(apiKey => apiKey.Key)
        );
    }

    [Fact]
    public void Should_RejectDuplicateAgentIds_When_ConfigHasDuplicateAgentIds()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateOptions(
                new Dictionary<string, string?>
                {
                    ["AgentAuthentication:Enabled"] = "true",
                    ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                    ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
                    ["AgentAuthentication:ApiKeys:1:AgentId"] = "agent-one",
                    ["AgentAuthentication:ApiKeys:1:Key"] = "secret-two",
                }
            )
        );

        Assert.Contains(
            "duplicate agent ids",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.DoesNotContain("secret-one", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-two", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_RejectDuplicateAgentKeys_When_ConfigHasDuplicateAgentKeys()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateOptions(
                new Dictionary<string, string?>
                {
                    ["AgentAuthentication:Enabled"] = "true",
                    ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                    ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
                    ["AgentAuthentication:ApiKeys:1:AgentId"] = "agent-two",
                    ["AgentAuthentication:ApiKeys:1:Key"] = "secret-one",
                }
            )
        );

        Assert.Contains(
            "duplicate agent keys",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.DoesNotContain("secret-one", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-one", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-two", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_RejectBlankAgentIds_When_ConfigHasBlankAgentId()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateOptions(
                new Dictionary<string, string?>
                {
                    ["AgentAuthentication:Enabled"] = "true",
                    ["AgentAuthentication:ApiKeys:0:AgentId"] = " ",
                    ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
                }
            )
        );

        Assert.Contains("blank agent id", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_RejectAgentIdsWithOuterWhitespace_When_ConfigHasAgentIdWithOuterWhitespace()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateOptions(
                new Dictionary<string, string?>
                {
                    ["AgentAuthentication:Enabled"] = "true",
                    ["AgentAuthentication:ApiKeys:0:AgentId"] = " agent-one",
                    ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
                }
            )
        );

        Assert.Contains("agent id", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("whitespace", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-one", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_RejectBlankAgentKeys_When_ConfigHasBlankAgentKey()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateOptions(
                new Dictionary<string, string?>
                {
                    ["AgentAuthentication:Enabled"] = "true",
                    ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                    ["AgentAuthentication:ApiKeys:0:Key"] = " ",
                }
            )
        );

        Assert.Contains("blank agent key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_RejectAgentKeysWithOuterWhitespace_When_ConfigHasAgentKeyWithOuterWhitespace()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateOptions(
                new Dictionary<string, string?>
                {
                    ["AgentAuthentication:Enabled"] = "true",
                    ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                    ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one ",
                }
            )
        );

        Assert.Contains("agent key", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("whitespace", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-one", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_RejectInvalidEnabledValue_When_ConfigHasInvalidEnabledValue()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateOptions(
                new Dictionary<string, string?>
                {
                    ["AgentAuthentication:Enabled"] = "not-a-bool",
                    ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                    ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
                }
            )
        );

        Assert.Contains("Enabled", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not-a-bool", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-one", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_FailClosed_When_AgentAuthIsEnabledWithNoValidKeys()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateOptions(
                new Dictionary<string, string?> { ["AgentAuthentication:Enabled"] = "true" }
            )
        );

        Assert.Contains("enabled", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "no valid agent keys",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Should_NotExposeSecrets_When_OptionsAreConvertedToString()
    {
        AgentAuthOptions options = CreateOptions(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        Assert.DoesNotContain("secret-one", options.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "secret-one",
            options.ApiKeys[0].ToString(),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "secret-one",
            JsonSerializer.Serialize(options),
            StringComparison.Ordinal
        );
        Assert.Contains("agent-one", options.ApiKeys[0].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Should_ExposeHeaderContract_When_ConstantsAreUsed()
    {
        Assert.Equal("X-Gatekeeper-Agent-Key", AgentAuthConstants.HeaderName);
    }

    private static AgentAuthOptions CreateOptions(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new AgentAuthOptions(configuration);
    }
}
