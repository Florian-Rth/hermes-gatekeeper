namespace Gatekeeper.Application.Common;

public sealed class AuthenticatedAgent
{
    public AuthenticatedAgent(string agentId, string authMethod)
    {
        AgentId = agentId;
        AuthMethod = authMethod;
    }

    public string AgentId { get; }

    public string AuthMethod { get; }
}
