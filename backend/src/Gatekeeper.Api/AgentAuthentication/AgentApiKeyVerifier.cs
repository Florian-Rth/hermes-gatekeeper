using System.Security.Cryptography;
using System.Text;

namespace Gatekeeper.Api.AgentAuthentication;

public sealed class AgentApiKeyVerifier
{
    private readonly AgentAuthOptions _options;

    public AgentApiKeyVerifier(AgentAuthOptions options)
    {
        _options = options;
    }

    public AgentAuthResult Verify(string? apiKey)
    {
        if (!_options.IsConfigured())
        {
            return AgentAuthResult.Failure(AgentAuthConstants.AuthNotConfiguredReason);
        }

        if (apiKey is null || apiKey.Length == 0)
        {
            return AgentAuthResult.Failure(AgentAuthConstants.MissingKeyReason);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AgentAuthResult.Failure(AgentAuthConstants.MalformedKeyReason);
        }

        AgentApiKeyOptions? matchedApiKey = null;

        foreach (AgentApiKeyOptions configuredApiKey in _options.ApiKeys)
        {
            if (FixedTimeKeyEquals(apiKey, configuredApiKey.Key))
            {
                matchedApiKey = configuredApiKey;
            }
        }

        if (matchedApiKey is null)
        {
            return AgentAuthResult.Failure(AgentAuthConstants.InvalidKeyReason);
        }

        return AgentAuthResult.Success(
            new AgentIdentity(matchedApiKey.AgentId, AgentAuthConstants.ApiKeyAuthMethod)
        );
    }

    private static bool FixedTimeKeyEquals(string presentedKey, string configuredKey)
    {
        byte[] presentedKeyHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));
        byte[] configuredKeyHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        return CryptographicOperations.FixedTimeEquals(presentedKeyHash, configuredKeyHash);
    }
}
