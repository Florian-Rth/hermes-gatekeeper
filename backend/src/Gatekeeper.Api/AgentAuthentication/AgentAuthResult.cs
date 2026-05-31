namespace Gatekeeper.Api.AgentAuthentication;

public sealed class AgentAuthResult
{
    private AgentAuthResult(bool succeeded, AgentIdentity? identity, string? failureReason)
    {
        Succeeded = succeeded;
        Identity = identity;
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }

    public AgentIdentity? Identity { get; }

    public string? FailureReason { get; }

    public static AgentAuthResult Success(AgentIdentity identity)
    {
        return new AgentAuthResult(true, identity, null);
    }

    public static AgentAuthResult Failure(string failureReason)
    {
        return new AgentAuthResult(false, null, failureReason);
    }

    public override string ToString()
    {
        if (Succeeded)
        {
            return $"AgentAuthResult {{ Succeeded = true, Identity = {Identity} }}";
        }

        return $"AgentAuthResult {{ Succeeded = false, FailureReason = {FailureReason} }}";
    }
}
