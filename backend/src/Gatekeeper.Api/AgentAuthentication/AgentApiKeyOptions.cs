using System.Text.Json.Serialization;

namespace Gatekeeper.Api.AgentAuthentication;

public sealed class AgentApiKeyOptions
{
    public AgentApiKeyOptions(string agentId, string key)
    {
        AgentId = agentId;
        Key = key;
    }

    public string AgentId { get; }

    [JsonIgnore]
    public string Key { get; }

    public override string ToString()
    {
        return $"AgentApiKeyOptions {{ AgentId = {AgentId}, Key = [redacted] }}";
    }
}
