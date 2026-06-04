using FastEndpoints;
using Gatekeeper.Api.AdminAuthentication;
using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Api.Endpoints.Sessions;

public sealed class RevokeSessionEndpoint : EndpointWithoutRequest<SessionLifecycleResponse>
{
    private readonly ISessionService _sessions;
    private readonly AdminSessionGuard _adminSessionGuard;

    public RevokeSessionEndpoint(ISessionService sessions, AdminSessionGuard adminSessionGuard)
    {
        _sessions = sessions;
        _adminSessionGuard = adminSessionGuard;
    }

    public override void Configure()
    {
        Post("/api/v1/sessions/{id}/revoke");
        AuthSchemes(AdminAuthConstants.Scheme);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_adminSessionGuard.HasValidUnsafeRequestOrigin(HttpContext))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (!Guid.TryParse(Route<string>("id"), out Guid id) || id == Guid.Empty)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        SessionLifecycleResult result = await _sessions.RevokeAsync(id, ct);
        if (result.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (result.Conflict || result.Session is null)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        await Send.OkAsync(SessionLifecycleResponse.FromDetails(result.Session), ct);
    }
}
