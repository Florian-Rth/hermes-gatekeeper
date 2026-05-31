using Gatekeeper.Api.AgentAuthentication;
using Microsoft.Extensions.Configuration;

namespace Gatekeeper.Tests;

public sealed class AgentAuthApiKeyVerifierTests
{
    [Fact]
    public void Should_ResolveAgentIdentity_When_KeyIsCorrect()
    {
        AgentApiKeyVerifier verifier = CreateVerifier(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        AgentAuthResult result = verifier.Verify("secret-one");

        Assert.True(result.Succeeded);
        Assert.Null(result.FailureReason);
        Assert.NotNull(result.Identity);
        Assert.Equal("agent-one", result.Identity.AgentId);
        Assert.Equal("apiKey", result.Identity.AuthMethod);
    }

    [Fact]
    public void Should_RejectWithInvalidKey_When_KeyIsUnknown()
    {
        AgentApiKeyVerifier verifier = CreateVerifier(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        AgentAuthResult result = verifier.Verify("wrong-secret");

        Assert.False(result.Succeeded);
        Assert.Null(result.Identity);
        Assert.Equal("invalid_key", result.FailureReason);
    }

    [Fact]
    public void Should_RejectWithAuthNotConfigured_When_AuthenticationIsDisabled()
    {
        AgentApiKeyVerifier verifier = CreateVerifier(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "false",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        AgentAuthResult result = verifier.Verify("secret-one");

        Assert.False(result.Succeeded);
        Assert.Null(result.Identity);
        Assert.Equal("auth_not_configured", result.FailureReason);
    }

    [Fact]
    public void Should_RejectWithMissingKey_When_KeyIsMissing()
    {
        AgentApiKeyVerifier verifier = CreateVerifier(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        AgentAuthResult result = verifier.Verify(null);

        Assert.False(result.Succeeded);
        Assert.Null(result.Identity);
        Assert.Equal("missing_key", result.FailureReason);
    }

    [Fact]
    public void Should_RejectWithMalformedKey_When_KeyIsBlank()
    {
        AgentApiKeyVerifier verifier = CreateVerifier(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        AgentAuthResult result = verifier.Verify("   ");

        Assert.False(result.Succeeded);
        Assert.Null(result.Identity);
        Assert.Equal("malformed_key", result.FailureReason);
    }

    [Fact]
    public void Should_RejectWithInvalidKey_When_KeyDiffersOnlyByCase()
    {
        AgentApiKeyVerifier verifier = CreateVerifier(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        AgentAuthResult result = verifier.Verify("SECRET-ONE");

        Assert.False(result.Succeeded);
        Assert.Null(result.Identity);
        Assert.Equal("invalid_key", result.FailureReason);
    }

    [Fact]
    public void Should_NotExposeSecretMaterial_When_VerificationFails()
    {
        AgentApiKeyVerifier verifier = CreateVerifier(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
            }
        );

        AgentAuthResult result = verifier.Verify("presented-secret");
        string resultText = result.ToString();

        Assert.False(result.Succeeded);
        Assert.DoesNotContain("secret-one", resultText, StringComparison.Ordinal);
        Assert.DoesNotContain("presented-secret", resultText, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-one", resultText, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_ResolveExactAgentId_When_MultipleKeysAreConfigured()
    {
        AgentApiKeyVerifier verifier = CreateVerifier(
            new Dictionary<string, string?>
            {
                ["AgentAuthentication:Enabled"] = "true",
                ["AgentAuthentication:ApiKeys:0:AgentId"] = "agent-one",
                ["AgentAuthentication:ApiKeys:0:Key"] = "secret-one",
                ["AgentAuthentication:ApiKeys:1:AgentId"] = "Agent-One",
                ["AgentAuthentication:ApiKeys:1:Key"] = "secret-two",
            }
        );

        AgentAuthResult result = verifier.Verify("secret-two");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Identity);
        Assert.Equal("Agent-One", result.Identity.AgentId);
        Assert.Equal("apiKey", result.Identity.AuthMethod);
    }

    private static AgentApiKeyVerifier CreateVerifier(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new AgentApiKeyVerifier(new AgentAuthOptions(configuration));
    }
}
