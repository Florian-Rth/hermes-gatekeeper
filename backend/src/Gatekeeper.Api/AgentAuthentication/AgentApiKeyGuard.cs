using Microsoft.Extensions.Primitives;

namespace Gatekeeper.Api.AgentAuthentication;

public sealed class AgentApiKeyGuard
{
    private readonly AgentApiKeyVerifier _verifier;

    public AgentApiKeyGuard(AgentApiKeyVerifier verifier)
    {
        _verifier = verifier;
    }

    public bool IsAuthenticated(HttpContext httpContext)
    {
        return Authenticate(httpContext).Succeeded;
    }

    public AgentAuthResult Authenticate(HttpContext httpContext)
    {
        string? apiKey = null;
        if (
            httpContext.Request.Headers.TryGetValue(
                AgentAuthConstants.HeaderName,
                out StringValues headerValues
            )
        )
        {
            if (headerValues.Count != 1)
            {
                return AgentAuthResult.Failure(AgentAuthConstants.MalformedKeyReason);
            }

            apiKey = headerValues[0];
        }

        return _verifier.Verify(apiKey);
    }
}
