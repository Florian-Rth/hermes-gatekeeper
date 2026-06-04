using System.Security.Claims;

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

    public static AgentIdentity? FromPrincipal(ClaimsPrincipal principal)
    {
        string? agentId = principal.FindFirstValue(AgentAuthConstants.AgentIdClaimType);
        string? authMethod = principal.FindFirstValue(AgentAuthConstants.AuthMethodClaimType);
        if (agentId is null || authMethod is null)
        {
            return null;
        }

        return new AgentIdentity(agentId, authMethod);
    }

    public override string ToString()
    {
        return $"AgentIdentity {{ AgentId = {AgentId}, AuthMethod = {AuthMethod} }}";
    }
}
