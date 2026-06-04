using System.Security.Claims;
using FastEndpoints;
using Gatekeeper.Api.AdminAuthentication;

namespace Gatekeeper.Api.Endpoints.Admin;

public sealed class AdminMeEndpoint : EndpointWithoutRequest<AdminSessionResponse>
{
    public override void Configure()
    {
        Get("/api/v1/admin/me");
        AuthSchemes(AdminAuthConstants.Scheme);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(
            new AdminSessionResponse
            {
                Authenticated = true,
                Username = HttpContext.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            },
            ct
        );
    }
}
