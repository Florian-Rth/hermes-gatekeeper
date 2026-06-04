using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gatekeeper.Api.AgentAuthentication;

public sealed class AgentApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AgentApiKeyGuard _guard;
    private string? _failureReason;

    public AgentApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AgentApiKeyGuard guard
    )
        : base(options, logger, encoder)
    {
        _guard = guard;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        AgentAuthResult result = _guard.Authenticate(Context);

        if (result.Succeeded && result.Identity is not null)
        {
            Claim[] claims =
            [
                new Claim(AgentAuthConstants.AgentIdClaimType, result.Identity.AgentId),
                new Claim(AgentAuthConstants.AuthMethodClaimType, result.Identity.AuthMethod),
            ];
            ClaimsIdentity identity = new ClaimsIdentity(claims, Scheme.Name);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            AuthenticationTicket ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        _failureReason = result.FailureReason ?? AgentAuthConstants.InvalidKeyReason;
        return Task.FromResult(AuthenticateResult.Fail(_failureReason));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties? properties)
    {
        string reason = _failureReason ?? AgentAuthConstants.MissingKeyReason;
        string routeTemplate = GetRouteTemplate();
        string httpMethod = Context.Request.Method;

        AgentAuthAuditWriter auditWriter =
            Context.RequestServices.GetRequiredService<AgentAuthAuditWriter>();
        await auditWriter.WriteFailedAuthenticationAsync(
            routeTemplate,
            httpMethod,
            reason,
            CancellationToken.None
        );

        Response.StatusCode = StatusCodes.Status401Unauthorized;
    }

    private string GetRouteTemplate()
    {
        Endpoint? endpoint = Context.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            return routeEndpoint.RoutePattern.RawText ?? Context.Request.Path;
        }

        return Context.Request.Path;
    }
}
