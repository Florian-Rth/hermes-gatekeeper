namespace Gatekeeper.Api.AgentAuthentication;

public sealed class AgentAuthOptions
{
    public AgentAuthOptions(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(AgentAuthConstants.SectionName);
        Enabled = ParseBool(section["Enabled"]);
        ApiKeys = LoadApiKeys(section).AsReadOnly();

        Validate();
    }

    public bool Enabled { get; }

    public IReadOnlyList<AgentApiKeyOptions> ApiKeys { get; }

    public bool IsConfigured()
    {
        return Enabled && ApiKeys.Count > 0;
    }

    public override string ToString()
    {
        return $"AgentAuthOptions {{ Enabled = {Enabled}, ApiKeyCount = {ApiKeys.Count} }}";
    }

    private static List<AgentApiKeyOptions> LoadApiKeys(IConfigurationSection section)
    {
        var apiKeys = new List<AgentApiKeyOptions>();
        foreach (IConfigurationSection keySection in section.GetSection("ApiKeys").GetChildren())
        {
            apiKeys.Add(
                new AgentApiKeyOptions(
                    keySection["AgentId"] ?? string.Empty,
                    keySection["Key"] ?? string.Empty
                )
            );
        }

        return apiKeys;
    }

    private void Validate()
    {
        foreach (AgentApiKeyOptions apiKey in ApiKeys)
        {
            if (string.IsNullOrWhiteSpace(apiKey.AgentId))
            {
                throw new InvalidOperationException(
                    "Agent authentication configuration contains a blank agent id."
                );
            }

            if (!string.Equals(apiKey.AgentId, apiKey.AgentId.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Agent authentication configuration contains an agent id with leading or trailing whitespace."
                );
            }

            if (string.IsNullOrWhiteSpace(apiKey.Key))
            {
                throw new InvalidOperationException(
                    "Agent authentication configuration contains a blank agent key."
                );
            }

            if (!string.Equals(apiKey.Key, apiKey.Key.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Agent authentication configuration contains an agent key with leading or trailing whitespace."
                );
            }
        }

        string[] duplicateAgentIds = ApiKeys
            .GroupBy(apiKey => apiKey.AgentId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateAgentIds.Length > 0)
        {
            throw new InvalidOperationException(
                "Agent authentication configuration contains duplicate agent ids."
            );
        }

        bool hasDuplicateKeys = ApiKeys
            .GroupBy(apiKey => apiKey.Key, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        if (hasDuplicateKeys)
        {
            throw new InvalidOperationException(
                "Agent authentication configuration contains duplicate agent keys."
            );
        }

        if (Enabled && ApiKeys.Count == 0)
        {
            throw new InvalidOperationException(
                "Agent authentication is enabled but no valid agent keys are configured."
            );
        }
    }

    private static bool ParseBool(string? value)
    {
        if (value is null)
        {
            return false;
        }

        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            "Agent authentication configuration contains an invalid Enabled value."
        );
    }
}
