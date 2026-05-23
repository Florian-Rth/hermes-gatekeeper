using FastEndpoints;
using Gatekeeper.Api.AdminTokens;
using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Api.Endpoints.Sessions;

public sealed class RevokeSessionEndpoint : EndpointWithoutRequest<SessionLifecycleResponse>
{
    private readonly ISessionService _sessions;
    private readonly IAdminTokenValidator _adminTokenValidator;

    public RevokeSessionEndpoint(ISessionService sessions, IAdminTokenValidator adminTokenValidator)
    {
        _sessions = sessions;
        _adminTokenValidator = adminTokenValidator;
    }

    public override void Configure()
    {
        Post("/api/v1/sessions/{id}/revoke");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        AdminTokenValidationResult tokenResult = _adminTokenValidator.Validate(
            HttpContext.Request.Headers
        );
        if (tokenResult == AdminTokenValidationResult.MissingHeader)
        {
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status401Unauthorized,
                cancellation: ct
            );
            return;
        }

        if (tokenResult == AdminTokenValidationResult.Forbidden)
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
