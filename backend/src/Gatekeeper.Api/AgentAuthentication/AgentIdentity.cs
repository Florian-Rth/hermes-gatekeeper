namespace Gatekeeper.Api.AgentAuthentication;

public sealed class AgentIdentity
{
    public AgentIdentity(string agentId, string authMethod)
    {
        AgentId = agentId;
        AuthMethod = authMethod;
    }

    public string AgentId { get; }

    public string AuthMethod { get; }

    public override string ToString()
    {
        return $"AgentIdentity {{ AgentId = {AgentId}, AuthMethod = {AuthMethod} }}";
    }
}
