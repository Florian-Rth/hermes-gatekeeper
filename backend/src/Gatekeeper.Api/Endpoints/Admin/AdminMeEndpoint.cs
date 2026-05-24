using FastEndpoints;
using Gatekeeper.Api.AdminAuthentication;

namespace Gatekeeper.Api.Endpoints.Admin;

public sealed class AdminMeEndpoint : EndpointWithoutRequest<AdminSessionResponse>
{
    private readonly AdminSessionGuard _guard;

    public AdminMeEndpoint(AdminSessionGuard guard)
    {
        _guard = guard;
    }

    public override void Configure()
    {
        Get("/api/v1/admin/me");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_guard.IsAuthenticated(HttpContext))
        {
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status401Unauthorized,
                cancellation: ct
            );
            return;
        }

        await Send.OkAsync(
            new AdminSessionResponse
            {
                Authenticated = true,
                Username = _guard.GetUsername(HttpContext),
            },
            ct
        );
    }
}
